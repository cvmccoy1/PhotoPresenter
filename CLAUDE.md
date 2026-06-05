# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build
dotnet build PhotoPresenter/PhotoPresenter.csproj

# Run (debug)
dotnet run --project PhotoPresenter/PhotoPresenter.csproj

# Open in Visual Studio 2022
start PhotoPresenter.sln
```

The project targets `net8.0-windows10.0.19041.0` with `UseWPF=true`. Only NuGet dependency is `CommunityToolkit.Mvvm 8.x`. The `10.0.19041.0` minimum is required for the WinRT `VideoProperties` API used for auto video rotation — do not lower it.

## Architecture

WPF MVVM app with two modes — **Organise** and **Present** — wired via a DataTemplate dispatch pattern in `App.xaml`. There is no navigation framework; `MainWindow` hosts a single `ContentControl` whose content switches between `OrganiseViewModel` and `PresentViewModel` instances, and WPF automatically applies the matching `DataTemplate` (defined without `x:Key` in `App.xaml.Resources`).

### Layer responsibilities

| Layer | Location | Role |
|-------|----------|------|
| Models | `Models/` | Pure data: `PhotoFolder`, `PhotoItem`, JSON sidecar DTOs |
| Services | `Services/` | `PhotoLibraryService` — scan folders, apply/save sidecar ordering; `UserSettings` — persist last folder, selected folder, window bounds, and splitter position to `%APPDATA%\PhotoPresenter\settings.json` |
| ViewModels | `ViewModels/` | All MVVM state; use `CommunityToolkit.Mvvm` `[ObservableProperty]` / `[RelayCommand]` source generators |
| Views | `Views/` + `MainWindow.xaml` | XAML layout + code-behind (only D&D event wiring and mouse event forwarding — no business logic) |

### Mode switching

`MainViewModel.CurrentMode` (enum `AppMode`) controls `CurrentView` (computed property). `MainWindow.cs` subscribes to `PropertyChanged` on `MainViewModel` and handles the `WindowStyle`/`WindowState` transition for fullscreen Present mode.

### Session persistence

`UserSettings` (`Services/UserSettings.cs`) stores: `LastParentFolder`, `LastSelectedFolder` (folder name), `LastSelectedPhoto` (filename), `PhotoScrollOffset` (double), window bounds, `WindowMaximized`, and `SplitterPosition`. All names are matched case-insensitively on restore. `Window_Closing` in `MainWindow.xaml.cs` is the authoritative save point for most settings — it reloads the file first to pick up any mid-session saves (e.g. splitter drags), then adds window bounds, `LastSelectedFolder`, and `LastSelectedPhoto` before writing. On startup, `MainViewModel` passes both saved names to `OrganiseViewModel.LoadAsync`, which restores the folder first (falling back to the first folder if not found), then restores the photo within that folder's active `Photos` collection (falling back to no selection if not found). `OnSelectedFolderChanged` resets `SelectedPhoto` to null synchronously, so the photo assignment in `LoadAsync` runs after that reset.

`PhotoScrollOffset` is saved and restored in `OrganiseView.xaml.cs`. `OnLoaded` subscribes to the parent window's `Closing` event (`OnWindowClosing`), which reads the `PhotoList` `ScrollViewer`'s `VerticalOffset` and saves it via the same load-update-save pattern. Restoration uses a `_pendingScrollOffset` field: `OnLoaded` reads the saved value and subscribes to `OrganiseViewModel.PropertyChanged`; when `SelectedFolder` is set by `LoadAsync`, `ScheduleScrollRestore` dispatches `ScrollToVerticalOffset` at `DispatcherPriority.Background` (after layout). A fallback in `OnLoaded` handles the race where `LoadAsync` completes before `OnLoaded` fires. The flag is cleared immediately so only the initial load triggers a restore.

### Sidecar file format

- `_photofolderorder.json` in parent folder — `{ "order": ["FolderName1", "FolderName2", ...] }`
- `_photoorder.json` in each subfolder — `{ "order": ["img001.jpg", "img002.jpg", ...] }`

Missing entries (renamed/deleted files) are silently skipped; unmentioned items append at the end in alphabetical / creation-date order.

### Undo

`OrganiseViewModel` maintains a `List<object>` undo stack (max 20 entries) of two snapshot record types: `FolderSnapshot` (ordered list of all folder VMs + `IsRemoved` flags) and `PhotoSnapshot` (folder identity + ordered list of all photo VMs + `IsRemoved` + `Caption`). Every mutating method calls `PushFolderUndo()` or `PushPhotoUndo()` before making changes. `Undo()` pops the top entry and reconstructs both the active and all-items collections from the snapshot, then re-saves the sidecar. Multi-select operations use bulk methods (`RemoveFolders`, `RestorePhotos`, `SetCaptions`, etc.) so the whole selection is one undo step. The stack is cleared in `LoadAsync()`. `CanUndo` (plain bool property with manual `OnPropertyChanged`) drives the toolbar button's `IsEnabled`. Ctrl+Z is handled in `MainWindow.Window_PreviewKeyDown` when in Organise mode.

### Key bindings (Present mode)

Handled in `MainWindow.Window_PreviewKeyDown`: `Right`/`Space` = next, `Left` = previous, `+`/`-` = zoom, `Escape` = back to Organise. Scroll wheel and right-click pan are handled in `PresentView.xaml.cs`.

### Zoom / Pan

`PresentView.xaml` applies a `TransformGroup` (`ScaleTransform` + `TranslateTransform`) with `RenderTransformOrigin="0.5,0.5"` directly bound to `ZoomScale`, `PanX`, `PanY` on `PresentViewModel`. Zoom resets to 1× on every photo navigation.

### Video rotation

`PresentViewModel.LoadCurrentPhotoAsync()` calls `GetVideoRotationAsync(path)` before setting `CurrentVideoPath`. This reads `Windows.Storage.FileProperties.VideoProperties.Orientation` (WinRT) and maps it to 0/90/180/270 degrees, which is assigned to `VideoRotation` (bound to the `MediaElement`'s `RotateTransform`). Falls back to 0 on any error. The manual ↻ 90° button adds to the auto-detected value. The TFM `net8.0-windows10.0.19041.0` is required for the WinRT API — do not lower it.

### Async image loading

`BitmapImage` is always created on a background `Task.Run`, `Freeze()`d, then assigned on the UI thread. Thumbnails use `DecodePixelWidth` (80px for folder cards, 150px for photo tiles). `PresentViewModel` preloads the next photo after each display. A monotonic `_loadSequence` counter guards against stale async results when the user navigates quickly.

### Thumbnail concurrency and caching

`PhotoItemViewModel.ThumbSemaphore` is `SemaphoreSlim(ProcessorCount - 2, min 4)` — scales with the machine rather than a fixed 6. Photo thumbnails are **lazy**: `PhotoItemViewModel` constructor does not fire thumbnail loading; `EnsureThumbnailLoaded()` is called by `PhotoFolderViewModel.LoadPhotoThumbnails()`, which `OrganiseViewModel.OnSelectedFolderChanged` invokes when a folder is selected. Folder-card thumbnails (left pane, ~40 total) still load immediately on construction. `ThumbnailCache` (`Services/ThumbnailCache.cs`) stores decoded thumbnails as JPEG in `%LOCALAPPDATA%\PhotoPresenter\thumbcache\` keyed by `{MD5(fullPath)}_{lastWriteTimeTicks}.jpg`. Cache hits bypass the semaphore entirely. Folder card thumbnails use a `|80` suffix on the key to distinguish their 80 px version from the 150 px photo version of the same file. Stale entries (different ticks for the same path) and files older than 90 days are pruned at startup on a background thread.

### Drag-and-drop

`OrganiseView.xaml.cs` implements WPF D&D for both lists (`PreviewMouseMove` → `DragDrop.DoDragDrop`; `Drop` → `OrganiseViewModel.ReorderFolders/ReorderPhotos`). The sidecar JSON is rewritten immediately after every reorder. Multi-selection drag uses a deferred-selection pattern: `PreviewMouseLeftButtonDown` suppresses the event (`e.Handled = true`) when clicking an already-selected item in a multi-selection, and `PreviewMouseLeftButtonUp` resolves it to a single selection only if no drag started.

`_folderDragCanStart` / `_photoDragCanStart` bool flags guard against accidental D&D triggered by scrollbar or empty-space clicks. The flags are set `true` only when `PreviewMouseLeftButtonDown` hits a `ListBoxItem`; `PreviewMouseMove` returns immediately if the flag is `false`, suppressing both the insertion-marker adorner and the drop.

### Open Folder in Explorer

Both pane context menus expose **Open Folder in Explorer** (added at the bottom after a separator). Folders pane: enabled only for a single-folder selection; `FolderTile_ContextMenuOpening` sets `IsEnabled=false` on `FolderOpenExplorer` when 2+ folders are selected. Photos/Videos pane: always enabled. `PhotoOpenExplorer_Click` dispatches to three paths based on the selection count: 0 selected → `explorer.exe "folderPath"`; 1 selected → `explorer.exe /select,"filePath"`; 2+ selected → `SHOpenFolderAndSelectItems` (shell32 P/Invoke, with plain folder-open as fallback). PIDs from `SHParseDisplayName` are freed via `CoTaskMemFree` in a `finally` block.

### Captions

`CaptionDialog` (`Views/CaptionDialog.xaml`) uses `AcceptsReturn="True"` with a `MaxHeight` so the TextBox grows as lines are added then scrolls. Plain Enter submits the dialog; Shift+Enter inserts a newline (the `KeyDown` handler checks `Keyboard.Modifiers` for Shift before treating Enter as OK). `NormalizeCaption` normalises `\r\n` to `\n` and trims surrounding whitespace before the caption is stored. Caption TextBlocks in both the Organise tile (`TextWrapping="Wrap"`, `TextAlignment="Center"`) and the Present mode overlay (`TextWrapping="Wrap"`, `TextAlignment="Center"`) render newlines naturally from the stored `\n` characters.

## Global usings

`GlobalUsings.cs` adds `System.IO`, `System.Windows.Media`, and `System.Windows.Media.Imaging` globally (required because the WPF SDK creates a temporary project for compilation that does not inherit all implicit usings).
