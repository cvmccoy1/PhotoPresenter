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

The UTC build date (formatted `MM-dd-yyyy`) is embedded as an `AssemblyMetadataAttribute` with key `"BuildDate"` via a top-level `PropertyGroup` + `ItemGroup` in `.csproj`. These must be at the **top level** (not inside a `<Target>`): `GenerateAssemblyInfo` collects `AssemblyAttribute` items during MSBuild's evaluation phase; items added dynamically inside a target run too late and are ignored. `AboutWindow.xaml.cs` reads the attribute at runtime via `Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()` and displays `"Version X.Y.Z  (built MM-dd-yyyy)"`. No extra packages or generated files are involved.

## Architecture

WPF MVVM app with two modes — **Organise** and **Present** — wired via a DataTemplate dispatch pattern in `App.xaml`. There is no navigation framework; `MainWindow` hosts a single `ContentControl` whose content switches between `OrganiseViewModel` and `PresentViewModel` instances, and WPF automatically applies the matching `DataTemplate` (defined without `x:Key` in `App.xaml.Resources`).

### Layer responsibilities

| Layer | Location | Role |
|-------|----------|------|
| Models | `Models/` | Pure data: `PhotoFolder`, `PhotoItem`, JSON sidecar DTOs |
| Services | `Services/` | `PhotoLibraryService` — scan folders, apply/save sidecar ordering; `UserSettings` — persist last folder, selected folder, window bounds, splitter position, theme, and text size to `%APPDATA%\PhotoPresenter\settings.json`; `ThemeService` — swap color and text-size ResourceDictionaries at runtime |
| ViewModels | `ViewModels/` | All MVVM state; use `CommunityToolkit.Mvvm` `[ObservableProperty]` / `[RelayCommand]` source generators |
| Views | `Views/` + `MainWindow.xaml` | XAML layout + code-behind (only D&D event wiring and mouse event forwarding — no business logic) |

### Mode switching

`MainViewModel.CurrentMode` (enum `AppMode`) controls `CurrentView` (computed property). `MainWindow.cs` subscribes to `PropertyChanged` on `MainViewModel` and handles the `WindowStyle`/`WindowState` transition for fullscreen Present mode.

### Session persistence

`UserSettings` (`Services/UserSettings.cs`) stores: `LastParentFolder`, `LastSelectedFolder` (folder name), `LastSelectedPhoto` (filename), `PhotoScrollOffset`, `FolderScrollOffset`, `ShowAllFolders`, `ShowAllPhotos`, `Volume` (Present mode, default 0.5), window bounds, `WindowMaximized`, `SplitterPosition`, `Theme` (default `"Light"`), and `TextSize` (default `"Normal"`). All names are matched case-insensitively on restore. `Window_Closing` in `MainWindow.xaml.cs` is the authoritative save point for most settings — it reloads the file first to pick up any mid-session saves (e.g. splitter drags), then adds window bounds, `LastSelectedFolder`, `LastSelectedPhoto`, `Theme`, and `TextSize` before writing. On startup, `MainViewModel` passes both saved names to `OrganiseViewModel.LoadAsync`, which restores the folder first (falling back to the first folder if not found), then restores the photo within that folder's active `Photos` collection (falling back to no selection if not found). `OnSelectedFolderChanged` resets `SelectedPhoto` to null synchronously, so the photo assignment in `LoadAsync` runs after that reset. `ShowAllFolders` and `ShowAllPhotos` are set on `OrganiseVM` immediately after firing `LoadAsync` (which suspends at its first `await`), so the flags are in place before the collections are populated on resume.

`PhotoScrollOffset` and `FolderScrollOffset` are saved and restored in `OrganiseView.xaml.cs`. `OnLoaded` subscribes to the parent window's `Closing` event (`OnWindowClosing`), which reads both `ScrollViewer` `VerticalOffset` values in a single load-update-save pass. Restoration uses `_pendingPhotoScrollOffset` and `_pendingFolderScrollOffset` fields (−1 = no restore pending): `OnLoaded` reads the saved values and subscribes to `OrganiseViewModel.PropertyChanged`; when `SelectedFolder` is set by `LoadAsync` (folders are already populated at that point), `SchedulePhotoScrollRestore` and `ScheduleFolderScrollRestore` each dispatch `ScrollToVerticalOffset` at `DispatcherPriority.Background` (after layout). A fallback in `OnLoaded` handles the race where `LoadAsync` completes before `OnLoaded` fires. Each flag is cleared immediately so only the initial load triggers a restore. `GetPhotoScrollViewer` / `GetFolderScrollViewer` walk one level into the list's visual tree (Border → ScrollViewer).

### Sidecar file format

- `_photofolderorder.json` in parent folder — `{ "order": ["FolderName1", "FolderName2", ...] }`
- `_photoorder.json` in each subfolder — `{ "order": ["img001.jpg", "img002.jpg", ...] }`

Missing entries (renamed/deleted files) are silently skipped; unmentioned items append at the end in alphabetical / creation-date order.

### Undo

`OrganiseViewModel` maintains a `List<object>` undo stack (max 20 entries) of two snapshot record types: `FolderSnapshot` (ordered list of all folder VMs + `IsRemoved` flags) and `PhotoSnapshot` (folder identity + ordered list of all photo VMs + `IsRemoved` + `Caption`). Every mutating method calls `PushFolderUndo()` or `PushPhotoUndo()` before making changes. `Undo()` pops the top entry and reconstructs both the active and all-items collections from the snapshot, then re-saves the sidecar. Multi-select operations use bulk methods (`RemoveFolders`, `RestorePhotos`, `SetCaptions`, etc.) so the whole selection is one undo step. The stack is cleared in `LoadAsync()`. `CanUndo` (plain bool property with manual `OnPropertyChanged`) drives the toolbar button's `IsEnabled`. Ctrl+Z is handled in `MainWindow.Window_PreviewKeyDown` when in Organise mode.

### Key bindings

Handled in `MainWindow.Window_PreviewKeyDown`: `F5` (Organise mode) = enter Present mode; `Ctrl+Z` (Organise mode) = undo. In Present mode: `Right`/`Space` = next, `Left` = previous, `+`/`-` = zoom, `Escape` = back to Organise. Scroll wheel and right-click pan are handled in `PresentView.xaml.cs`.

### Zoom / Pan

`PresentView.xaml` applies a `TransformGroup` (`ScaleTransform` + `TranslateTransform`) with `RenderTransformOrigin="0.5,0.5"` directly bound to `ZoomScale`, `PanX`, `PanY` on `PresentViewModel`. Zoom resets to 1× on every photo navigation.

### Video rotation

`PresentViewModel.LoadCurrentPhotoAsync()` calls `GetVideoRotationAsync(path)` before setting `CurrentVideoPath`. This reads `Windows.Storage.FileProperties.VideoProperties.Orientation` (WinRT) and maps it to 0/90/180/270 degrees, which is assigned to `VideoRotation` (bound to the `MediaElement`'s `RotateTransform`). Falls back to 0 on any error. The manual ↻ 90° button adds to the auto-detected value. The TFM `net8.0-windows10.0.19041.0` is required for the WinRT API — do not lower it.

### Video error handling

`PresentView.xaml.cs` tracks a `_mediaFailed` bool flag alongside the `MediaElement` events. When WMF fires `MediaFailed` (e.g. an unsupported audio codec variant), the handler sets `_mediaFailed = true` and shows an amber `VideoErrorBanner` (`Border` + `TextBlock` overlay, top-centre of the screen) with the message "Unable to play video — codec may not be installed". Crucially, it does **not** reset `IsPlaying` or stop the position timer, because WMF may still recover and fire `MediaOpened` for the video track.

`VideoPlayer_MediaOpened` retries `VideoPlayer.Play()` if `Vm.IsPlaying == true` (belt-and-suspenders for both the WMF-recovery case and normal HEVC timing races where the initial `Play()` is dropped before the codec initialises). It then checks `_mediaFailed || !VideoPlayer.HasAudio`: if either is true the banner is updated to "Playing without audio — audio codec may not be installed". `HideVideoError()` is called — clearing `_mediaFailed` and collapsing the banner — at the start of every new video load (`CurrentVideoPath` change) and when switching away from video (`CurrentIsVideo = false`). For videos that fail completely (no WMF recovery, `MediaOpened` never fires), the banner remains visible and `IsPlaying` stays true but the position timer tick exits early because `NaturalDuration.HasTimeSpan` is false.

### Photo EXIF orientation

WPF's `BitmapImage` silently ignores the EXIF orientation tag (0x0112), so `PhotoItemViewModel.LoadBitmap` applies it manually. For non-HEIC formats, `ReadExifOrientation` opens the file with `BitmapDecoder.Create` + `DelayCreation`/`OnDemand` (header-only read), queries `/app1/ifd/{ushort=274}` then `/xmp/exif:Orientation` as a fallback, and returns the integer value. `ApplyExifOrientation` then wraps the decoded `BitmapSource` in one or two `TransformedBitmap` steps — orientations 2–4/6/8 need a single `ScaleTransform` or `RotateTransform`; orientations 5 and 7 need a `RotateTransform` followed by a horizontal `ScaleTransform(-1,1)`. For HEIC/HEIF, orientation is read directly from `frame.Metadata` inside `LoadViaDecoder` (which already uses `BitmapDecoder`). The thumbnail cache stores the already-corrected bitmap, so the orientation read adds one extra file-open only on the first visit per file.

### Async image loading

`BitmapImage` is always created on a background `Task.Run`, `Freeze()`d, then assigned on the UI thread. Thumbnails use `DecodePixelWidth` (80px for folder cards, 150px for photo tiles). `PresentViewModel` preloads the next photo after each display. A monotonic `_loadSequence` counter guards against stale async results when the user navigates quickly.

### Thumbnail concurrency and caching

`PhotoItemViewModel.ThumbSemaphore` is `SemaphoreSlim(ProcessorCount - 2, min 4)` — scales with the machine rather than a fixed 6. Photo thumbnails are **lazy**: `PhotoItemViewModel` constructor does not fire thumbnail loading; `EnsureThumbnailLoaded()` is called by `PhotoFolderViewModel.LoadPhotoThumbnails()`, which `OrganiseViewModel.OnSelectedFolderChanged` invokes when a folder is selected. Folder-card thumbnails (left pane, ~40 total) still load immediately on construction. `ThumbnailCache` (`Services/ThumbnailCache.cs`) stores decoded thumbnails as JPEG in `%LOCALAPPDATA%\PhotoPresenter\thumbcache\` keyed by `{MD5(fullPath)}_{lastWriteTimeTicks}.jpg`. Cache hits bypass the semaphore entirely. Folder card thumbnails use a `|80` suffix on the key to distinguish their 80 px version from the 150 px photo version of the same file. Stale entries (different ticks for the same path) and files older than 90 days are pruned at startup on a background thread.

### Drag-and-drop

`OrganiseView.xaml.cs` implements WPF D&D for both lists (`PreviewMouseMove` → `DragDrop.DoDragDrop`; `Drop` → `OrganiseViewModel.ReorderFolders/ReorderPhotos`). The sidecar JSON is rewritten immediately after every reorder. Multi-selection drag uses a deferred-selection pattern: `PreviewMouseLeftButtonDown` suppresses the event (`e.Handled = true`) when clicking an already-selected item in a multi-selection, and `PreviewMouseLeftButtonUp` resolves it to a single selection only if no drag started.

`_folderDragCanStart` / `_photoDragCanStart` bool flags guard against accidental D&D triggered by scrollbar or empty-space clicks. The flags are set `true` only when `PreviewMouseLeftButtonDown` hits a `ListBoxItem`; `PreviewMouseMove` returns immediately if the flag is `false`, suppressing both the insertion-marker adorner and the drop.

`HitsScrollBar(UIElement, Point)` (static helper in `OrganiseView.xaml.cs`) walks the visual tree from `InputHitTest` upward looking for a `ScrollBar`. Both `FolderList_PreviewMouseLeftButtonDown` and `PhotoList_PreviewMouseLeftButtonDown` call it before invoking `UnselectAll()` when the hit misses all `ListBoxItem`s — scrollbar clicks are ignored rather than treated as empty-space clicks, so the folder selection (and the photo pane contents) are preserved while dragging the scrollbar thumb.

### Open Folder in Explorer

Both pane context menus expose **Open Folder in Explorer** (added at the bottom after a separator). Folders pane: enabled only for a single-folder selection; `FolderTile_ContextMenuOpening` sets `IsEnabled=false` on `FolderOpenExplorer` when 2+ folders are selected. Photos/Videos pane: always enabled. `PhotoOpenExplorer_Click` dispatches to three paths based on the selection count: 0 selected → `explorer.exe "folderPath"`; 1 selected → `explorer.exe /select,"filePath"`; 2+ selected → `SHOpenFolderAndSelectItems` (shell32 P/Invoke, with plain folder-open as fallback). PIDs from `SHParseDisplayName` are freed via `CoTaskMemFree` in a `finally` block.

### Theming

The toolbar is a `Grid` (`x:Name="MainToolbar"`) with two columns: a `*`-width left `ToolBarTray` (Browse Folder, path, Undo, Theme dropdown, Text dropdown, About) and an `Auto`-width right `ToolBarTray` (▶ Present, right-justified). `MainToolbar.Visibility` is toggled in `ApplyMode` when switching to/from Present mode. The toolbar exposes two independent dropdowns — **Theme** (color) and **Text** (size) — wired to two separate `MergedDictionaries` slots in `App.xaml`: `[0]` = color theme, `[1]` = text size. `App.OnStartup` (`App.xaml.cs`) calls `ThemeService.ApplyColor(settings.Theme)` then `ThemeService.ApplyTextSize(settings.TextSize)` before the window appears so there is no flash. `ThemeService` (`Services/ThemeService.cs`) has two methods that each replace the corresponding slot; all `DynamicResource` bindings update live.

**Color themes** (`PhotoPresenter/Themes/*.xaml` — 9 files): Light, Dark, HighContrastLight, HighContrastDark, SlateBlue, Forest, Sunset, Amethyst, Teal. Each defines only `SolidColorBrush` resources: ten named brushes (`AppBackground`, `PanelBackground`, `PanelText`, `SplitterBackground`, `ListBackground`, `ThumbnailBackground`, `VideoOverlayBackground`, `VideoIconForeground`, `FilenameForeground`, `CaptionForeground`) plus four `SystemColors` ListBox-selection key overrides. The colorful themes (SlateBlue, Forest, Sunset, Amethyst, Teal) additionally override `SystemColors.MenuBrushKey/MenuTextBrushKey/MenuHighlightBrushKey/MenuBarBrushKey` so context menus adopt the palette.

**Text size files** (`PhotoPresenter/Themes/TextSize_*.xaml` — 4 files): `TextSize_Small.xaml`, `TextSize_Normal.xaml`, `TextSize_Large.xaml`, `TextSize_XLarge.xaml`. Each defines only the eight `sys:Double` font-size resources (`FontSize.XSmall/Small/Medium/Normal/Large`, `FontSize.PresentCaption/PresentLoading/VideoIcon`). Approximate scale: Small ≈ 85%, Normal = baseline, Large ≈ 130%, Extra Large ≈ 160%.

`OrganiseView.xaml` binds all colors and font sizes via `DynamicResource`; `PresentView.xaml` binds only font sizes (Present mode always has a black backdrop). Both `FolderList` and `PhotoList` set `Background="{DynamicResource ListBackground}"` explicitly — without this the ListBox renders with `SystemColors.WindowBrush` (white) regardless of theme. Folder name TextBlocks carry an explicit `Foreground="{DynamicResource FilenameForeground}"` for the same reason. Both ComboBoxes wire `SelectionChanged` in code-behind after `InitThemeComboBox()` / `InitTextComboBox()` set the initial selections, preventing spurious saves on startup.

### Mirror

`IsMirrored` (bool) lives on `PhotoItem` (model) and `PhotoItemViewModel`. Toggling is a user gesture — it is never inferred from file metadata and never baked into the decoded bitmap. Mirroring is therefore a pure display transform applied at render time:

- **Organise thumbnail**: a `DataTrigger` on `IsMirrored` sets a `ScaleTransform(ScaleX=-1)` on the photo `Image` element (with `RenderTransformOrigin="0.5,0.5"`). Videos are not mirrored in the thumbnail (the play-icon overlay is a sibling element and is unaffected).
- **Present mode photo**: the `Image`'s `RenderTransform` is a `TransformGroup` containing the zoom `ScaleTransform` followed by a mirror `ScaleTransform(ScaleX=MirrorScaleX)`. `MirrorScaleX` is a computed property on `PresentViewModel` that returns −1.0 or 1.0.
- **Present mode video**: the `MediaElement`'s `RenderTransform` is a `TransformGroup` with the mirror `ScaleTransform` applied **first**, then the auto-rotation `RotateTransform`. This order flips the final on-screen image horizontally regardless of the rotation angle.

`PresentViewModel.CurrentIsMirrored` is set from `photo.IsMirrored` at the top of `LoadCurrentPhotoAsync` (before the video/photo branch), and reset to `false` in `SetFolders`. `MirrorScaleX` notifies via `[NotifyPropertyChangedFor]` on `CurrentIsMirrored`.

Context menu: "Mirror" (tag `Mirror`) is shown when `IsMirrored=False`; "Remove Mirror" (tag `RemoveMirror`) is shown when `IsMirrored=True`. Both items sit before "Remove from Presentation". `PhotoTile_ContextMenuOpening` overrides their visibility for multi-select: if any selected items are not mirrored, "Mirror" is shown; if any are mirrored, "Remove Mirror" is shown (both can appear simultaneously for mixed selections). `PhotoMirror_Click` distinguishes the two by checking `sender.Tag == "Mirror"` and passes only the applicable subset to `OrganiseViewModel.ToggleMirrors`, which pushes a photo undo snapshot and saves the sidecar.

Persistence: stored in `_photoorder.json` under a `"mirrored"` key (list of filenames); the key is omitted entirely when no items are mirrored. `PhotoOrderSidecar.Mirrored` is `List<string>?`; `PhotoLibraryService.ToPhotoItem` accepts a `HashSet<string>? mirrored` and sets `IsMirrored` on load. Tooltip resets (`_toolTipLoaded = false`) in `OnIsMirroredChanged` so "Mirrored: Yes" appears on next hover; not shown when `IsMirrored` is false.

### Live file system sync

`OrganiseViewModel` owns two `FileSystemWatcher` instances:

- `_parentWatcher` — watches the parent folder with `NotifyFilter = NotifyFilters.DirectoryName`, `IncludeSubdirectories = false`. Handles subfolder `Renamed` (updates `PhotoFolderViewModel` name/paths via `UpdatePath`, rewrites the sidecar to the new path, redirects `_folderWatcher` if the renamed folder is the selected one), `Deleted` (removes the VM from both `_allFolderItems` and `Folders`, shows a 5-second amber status banner), and `Created` (waits 150 ms, verifies `Directory.Exists`, adds a new `PhotoFolderViewModel` at the end of the active section).
- `_folderWatcher` — watches the currently selected subfolder with `NotifyFilter = NotifyFilters.FileName`. Handles file `Created` (waits 150 ms, verifies `File.Exists`, checks media extension via `PhotoLibraryService.IsMediaFile`, adds a new `PhotoItemViewModel`), `Deleted` (removes the VM), and `Renamed` — two sub-cases: if a VM exists for the old path, calls `UpdatePath`; if no VM exists (e.g. an encoder wrote to a non-media temp file then renamed it to a media file), treats it as a new addition identical to `Created`.

When a new `PhotoItemViewModel` is added via either FSW handler, `EnsureThumbnailLoaded()` is called immediately, followed by `RetryThumbnailAfterDelayAsync()`. The retry loop (`PhotoItemViewModel`) waits at cumulative intervals of 2 s, 5 s, 10 s, 20 s, and 35 s, calling `LoadThumbnailAsync()` at each step and stopping as soon as a non-null thumbnail is returned. This handles two cases: (1) the file arrived via a temp-file rename so encoding is complete but Windows Shell hasn't generated the video thumbnail yet; (2) the file arrived via `Created` while encoding was still in progress, so the video stream wasn't readable until encoding finished.

All FSW callbacks arrive on a thread-pool thread and dispatch to the UI thread via `Application.Current.Dispatcher.InvokeAsync`. `UpdateFolderWatcher(string?)` is called from `OnSelectedFolderChanged`; `StartParentWatcher(string)` is called at the end of `LoadAsync`. `OrganiseViewModel` implements `IDisposable`; `Window_Closing` in `MainWindow.xaml.cs` calls `Dispose()` before saving settings.

`PhotoFolderViewModel.UpdatePath(newName, newFullPath)` updates `Model.Name`, `Model.FullPath`, cascades to all child `PhotoItemViewModel`s, and raises `PropertyChanged` for `Name`, `FullPath`, and `FolderToolTipText`. `PhotoItemViewModel.UpdatePath(newFileName, newFullPath)` updates `Model.FileName`, `Model.FullPath`, and raises `PropertyChanged` for both.

`SaveAllPhotoOrder` and `SaveAllFolderOrder` are wrapped in `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)` — on failure they call `ShowStatus(message)` which sets `StatusMessage` and starts a `DispatcherTimer` to clear it after 5 seconds. The amber status banner in `OrganiseView.xaml` (docked above the two-pane area) is bound to `HasStatusMessage` via `BooleanToVisibilityConverter` (`x:Key="BoolToVis"` in `App.xaml`); a dismiss button (✕) sets `StatusMessage = null`.

### Captions

`CaptionDialog` (`Views/CaptionDialog.xaml`) uses `AcceptsReturn="True"` with a `MaxHeight` so the TextBox grows as lines are added then scrolls. Plain Enter submits the dialog; Shift+Enter inserts a newline (the `KeyDown` handler checks `Keyboard.Modifiers` for Shift before treating Enter as OK). `NormalizeCaption` normalises `\r\n` to `\n` and trims surrounding whitespace before the caption is stored. Caption TextBlocks in both the Organise tile (`TextWrapping="Wrap"`, `TextAlignment="Center"`) and the Present mode overlay (`TextWrapping="Wrap"`, `TextAlignment="Center"`) render newlines naturally from the stored `\n` characters.

The `CaptionBox` TextBox has `SpellCheck.IsEnabled="True"` (set in XAML). The code-behind constructor sets `CaptionBox.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag)` so the correct Windows dictionary is used (e.g. en-GB, fr-FR). This gives red squiggly underlines and right-click correction suggestions with no additional packages.

## Global usings

`GlobalUsings.cs` adds `System.IO`, `System.Windows.Media`, and `System.Windows.Media.Imaging` globally (required because the WPF SDK creates a temporary project for compilation that does not inherit all implicit usings).
