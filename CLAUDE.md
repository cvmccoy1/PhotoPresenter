# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build Windows app
dotnet build PhotoPresenter/PhotoPresenter.csproj

# Run Windows app (debug)
dotnet run --project PhotoPresenter/PhotoPresenter.csproj

# Build Android companion app
dotnet build PhotoPresenterAndroid/PhotoPresenterAndroid.csproj

# Open solution in Visual Studio 2022 (covers both projects)
start PhotoPresenter.sln
```

The Windows project targets `net8.0-windows10.0.19041.0` with `UseWPF=true`; its only NuGet dependency is `CommunityToolkit.Mvvm 8.x`. The `10.0.19041.0` minimum is required for the WinRT `VideoProperties` API used for auto video rotation — do not lower it. The Android project targets `net9.0-android` with `UseMaui=true`, minimum API 34; its key dependencies are `CommunityToolkit.Maui`, `CommunityToolkit.Maui.MediaElement`, and `Xamarin.AndroidX.LocalBroadcastManager` (see the Android companion app section below).

The UTC build date (formatted `MM-dd-yyyy`) is embedded as an `AssemblyMetadataAttribute` with key `"BuildDate"` via a top-level `PropertyGroup` + `ItemGroup` in `.csproj`. These must be at the **top level** (not inside a `<Target>`): `GenerateAssemblyInfo` collects `AssemblyAttribute` items during MSBuild's evaluation phase; items added dynamically inside a target run too late and are ignored. `AboutWindow.xaml.cs` reads the attribute at runtime via `Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()` and displays `"Version X.Y.Z  (built MM-dd-yyyy)"`. No extra packages or generated files are involved.

## Development workflow

- Any new feature or behavior change must come with corresponding test coverage in `PhotoPresenter.Tests/` (see `## Tests` below for structure and conventions).
- Before considering a change complete, run the full suite — `dotnet test PhotoPresenter.Tests/PhotoPresenter.Tests.csproj` — and confirm all tests pass.
- Before implementing any requested change, ask clarifying questions until at least 99% sure of what is being asked — even if it takes multiple rounds of questions. Only skip this when the request is already fully unambiguous.

## Architecture

WPF MVVM app with two modes — **Organise** and **Present** — wired via a DataTemplate dispatch pattern in `App.xaml`. There is no navigation framework; `MainWindow` hosts a single `ContentControl` whose content switches between `OrganiseViewModel` and `PresentViewModel` instances, and WPF automatically applies the matching `DataTemplate` (defined without `x:Key` in `App.xaml.Resources`).

### Layer responsibilities

| Layer | Location | Role |
|-------|----------|------|
| Models | `Models/` | Pure data: `PhotoFolder`, `PhotoItem`, JSON sidecar DTOs |
| Services | `Services/` | `PhotoLibraryService` — scan folders, apply/save sidecar ordering; `UserSettings` — persist last folder, selected folder, window bounds, splitter position, theme, and text size to `%APPDATA%\PhotoPresenter\settings.json`; `ThemeService` — swap color and text-size ResourceDictionaries at runtime; `ShellInterop` / `ShellFolderPicker` / `ShellFileOperation` — COM/P/Invoke layer for shell-namespace-aware folder picking and file I/O (MTP phone support) |
| ViewModels | `ViewModels/` | All MVVM state; use `CommunityToolkit.Mvvm` `[ObservableProperty]` / `[RelayCommand]` source generators |
| Views | `Views/` + `MainWindow.xaml` | XAML layout + code-behind (only D&D event wiring and mouse event forwarding — no business logic) |

### Mode switching

`MainViewModel.CurrentMode` (enum `AppMode`) controls `CurrentView` (computed property). `MainWindow.cs` subscribes to `PropertyChanged` on `MainViewModel` and handles the `WindowStyle`/`WindowState` transition for fullscreen Present mode.

`SwitchToPresent()` checks `MainViewModel.FavoritesOnly`. When false, it captures `OrganiseVM.SelectedFolder` / `SelectedPhoto` and passes them as start indices to `PresentVM.SetFolders` (standard path). When true, it compacts the folder list: for each non-empty folder it builds a `List<PhotoItemViewModel>` of only the `IsFavorite` items, then calls `PresentVM.SetFolders` with those folders plus a parallel `effectivePhotos` list. If no favorites exist anywhere, it calls `OrganiseVM.ShowStatus("No favorites to present.")` and aborts. The start position in favorites mode is the first favorite on or after the selected photo (or the first favorite overall if none matches). `SwitchToOrganise()` reads `PresentVM.CurrentFolder` / `CurrentPhotoItem` (read-only properties derived from the current index fields) and writes them back to `OrganiseVM.SelectedFolder` / `SelectedPhoto`, then sets `OrganiseVM.ScrollPhotoIntoViewRequested = true` before switching mode. `OrganiseView.OnLoaded` checks the flag (the view is recreated each mode switch), resets it, and dispatches `FolderList.ScrollIntoView` + `PhotoList.ScrollIntoView` at `DispatcherPriority.Background` — this runs after any saved-offset restore and overrides it, leaving both panes scrolled to show the last-viewed item.

### Session persistence

`UserSettings` (`Services/UserSettings.cs`) stores: `LastParentFolder`, `LastSelectedFolder` (folder name), `LastSelectedPhoto` (filename), `PhotoScrollOffset`, `FolderScrollOffset`, `ShowAllFolders`, `ShowAllPhotos`, `Volume` (Present mode, default 0.5), `FadeTransitionEnabled` (default `true`), `AutoplayIntervalSeconds` (default `5`), window bounds, `WindowMaximized`, `SplitterPosition`, `Theme` (default `"Light"`), and `TextSize` (default `"Normal"`). All names are matched case-insensitively on restore. `Window_Closing` in `MainWindow.xaml.cs` is the authoritative save point for most settings — it reloads the file first to pick up any mid-session saves (e.g. splitter drags), then adds window bounds, `LastSelectedFolder`, `LastSelectedPhoto`, `Theme`, and `TextSize` before writing. On startup, `MainViewModel` passes both saved names to `OrganiseViewModel.LoadAsync`, which restores the folder first (falling back to the first folder if not found), then restores the photo within that folder's active `Photos` collection (falling back to no selection if not found). `OnSelectedFolderChanged` resets `SelectedPhoto` to null synchronously, so the photo assignment in `LoadAsync` runs after that reset. `ShowAllFolders` and `ShowAllPhotos` are set on `OrganiseVM` immediately after firing `LoadAsync` (which suspends at its first `await`), so the flags are in place before the collections are populated on resume.

`PhotoScrollOffset` and `FolderScrollOffset` are saved and restored in `OrganiseView.xaml.cs`. `OnLoaded` subscribes to the parent window's `Closing` event (`OnWindowClosing`), which reads both `ScrollViewer` `VerticalOffset` values in a single load-update-save pass. Restoration uses `_pendingPhotoScrollOffset` and `_pendingFolderScrollOffset` fields (−1 = no restore pending): `OnLoaded` reads the saved values and subscribes to `OrganiseViewModel.PropertyChanged`; when `SelectedFolder` is set by `LoadAsync` (folders are already populated at that point), `SchedulePhotoScrollRestore` and `ScheduleFolderScrollRestore` each dispatch `ScrollToVerticalOffset` at `DispatcherPriority.Background` (after layout). A fallback in `OnLoaded` handles the race where `LoadAsync` completes before `OnLoaded` fires. Each flag is cleared immediately so only the initial load triggers a restore. `GetPhotoScrollViewer` / `GetFolderScrollViewer` walk one level into the list's visual tree (Border → ScrollViewer).

### Sidecar file format

- `_photofolderorder.json` in parent folder — `{ "order": [...], "removed": [...] }`
- `_photoorder.json` in each subfolder — all keys except `"order"` are optional and omitted when empty:
  ```json
  {
    "order":       ["img001.jpg", ...],
    "removed":     ["img003.jpg", ...],
    "mirrored":    ["img002.jpg", ...],
    "favorites":   ["img001.jpg", ...],
    "captions":    { "img001.jpg": "Caption text" },
    "adjustments": { "img002.jpg": { "brightness": 10, "contrast": -5 } }
  }
  ```

Missing entries (renamed/deleted files) are silently skipped; unmentioned items append at the end in alphabetical / creation-date order.

### Undo

`OrganiseViewModel` maintains a `List<object>` undo stack (max 20 entries) of two snapshot record types: `FolderSnapshot` (ordered list of all folder VMs + `IsRemoved` flags) and `PhotoSnapshot` (folder identity + ordered list of all photo VMs + `IsRemoved` + `Caption` + `IsMirrored` + `IsFavorite` + `Brightness` + `Contrast`). Every mutating method calls `PushFolderUndo()` or `PushPhotoUndo()` before making changes. `Undo()` pops the top entry and reconstructs both the active and all-items collections from the snapshot, then re-saves the sidecar. Multi-select operations use bulk methods (`RemoveFolders`, `RestorePhotos`, `SetCaptions`, etc.) so the whole selection is one undo step. The stack is cleared in `LoadAsync()`. `CanUndo` (plain bool property with manual `OnPropertyChanged`) drives the toolbar button's `IsEnabled`. Ctrl+Z is handled in `MainWindow.Window_PreviewKeyDown` when in Organise mode.

### Key bindings

Handled in `MainWindow.Window_PreviewKeyDown`: `F5` (Organise mode) = enter Present mode; `Space` (Organise mode) = enter Present mode, but only if `Keyboard.FocusedElement` is not a `ButtonBase`, `ComboBox`, or `TextBoxBase` (so toolbar controls still receive Space normally); `Ctrl+Z` (Organise mode) = undo; `?` (both modes) = open `ShortcutsWindow`. In Present mode: `Right`/`Space` = next, `Left` = previous, `+`/`-` = zoom, `P` = toggle `IsAutoplayEnabled`, `Escape` = back to Organise (syncs last-viewed folder/photo back to Organise mode). Scroll wheel and right-click pan are handled in `PresentView.xaml.cs`.

### Overall counter

`PresentViewModel` exposes `OverallLabel` (e.g. `"759 of 1956"`) showing the global position of the current item across all folders. `SetFolders` precomputes `_cumulativeCounts` (a `int[]` where `_cumulativeCounts[i]` = total photos in folders 0…i-1) and `_totalPhotoCount` in O(n) so that `UpdateLabels` can derive the overall 1-based position in O(1): `_cumulativeCounts[_currentFolderIndex] + _currentPhotoIndex + 1`. `UpdateLabels` is called synchronously by `SetFolders` and at the end of each `LoadCurrentPhotoAsync` completion. The label is shown in both the photo overlay (bottom-left) and the video controls bar.

### Video scrub bar

`PresentView.xaml.cs` wires five handlers on `ScrubSlider` (a `Slider` with `LoadedBehavior="Manual"` `MediaElement` as the target):

- `Thumb.DragStartedEvent` / `DragCompletedEvent` (via `AddHandler`) — set/clear `_isDragging`; `DragCompleted` seeks and calls `VideoPlayer.Play()` if `Vm.IsPlaying`.
- `PreviewMouseLeftButtonDown` — distinguishes thumb from track using `IsThumbHit` (walks the visual tree looking for a `Thumb` ancestor). For **track clicks**: uses `Track.ValueFromPoint(e.GetPosition(track))` on `PART_Track` to jump to the exact clicked position (suppressing the RepeatButton's `LargeChange` command via `e.Handled = true`), seeks `VideoPlayer.Position`, calls `VideoPlayer.Play(); VideoPlayer.Pause()` if paused (forces WMF to render the seeked frame — a Position change alone does not update the display when paused), and calls `Vm.UpdatePosition()` directly. For **thumb clicks**: sets `_isDragging = true` only; DragStarted/DragCompleted own the drag.
- `ValueChanged` — live scrub during thumb drag: seeks and force-renders the frame while paused; also calls `Vm.UpdatePosition()` directly so the time label updates when the position timer is stopped (e.g. after `MediaEnded`).

`MediaEnded` stops the position timer so the slider stays at the end position rather than snapping to 0. The `IsPlaying = true` branch of `OnVmPropertyChanged` calls `_positionTimer.Start()` to restart it when the user plays again after a completed video. **Important**: `ScrubSlider.PreviewMouseLeftButtonUp` must NOT be subscribed — in every attempt it has broken WMF video playback. The exact interaction is opaque, but the pattern is consistent.

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

The toolbar is a `Grid` (`x:Name="MainToolbar"`) with two columns: a `*`-width left `StackPanel` (containing a `ToolBarTray` with Undo, Settings…, Export Favorites…, About buttons) and an `Auto`-width right `StackPanel` (Favorites Only `CheckBox` + ▶ Present `Button`, right-justified). A second `ToolBarTray` below holds the Parent Folder… button and folder path. `MainToolbar.Visibility` is toggled in `ApplyMode` when switching to/from Present mode.

Theme and Text Size are configured in `SettingsWindow.xaml` (opened via Settings… button), which receives the `MainViewModel` instance so its `PresentVM.IsFadeEnabled` / `PresentVM.AutoplayIntervalSeconds` bindings work. `SettingsWindow` uses the same `InitThemeComboBox()` / `InitTextComboBox()` + deferred-`SelectionChanged` pattern as the former inline toolbar controls: selections are set before wiring `SelectionChanged`, which immediately calls `ThemeService.ApplyColor` / `ThemeService.ApplyTextSize` and saves settings, preventing spurious saves on open. The autoplay interval ComboBox uses `SelectedValue="{Binding PresentVM.AutoplayIntervalSeconds}"` two-way and is saved at `Window_Closing` (same as `IsFadeEnabled`). `App.OnStartup` (`App.xaml.cs`) calls `ThemeService.ApplyColor(settings.Theme)` then `ThemeService.ApplyTextSize(settings.TextSize)` before the main window appears so there is no flash. `ThemeService` (`Services/ThemeService.cs`) has two methods that each replace the corresponding `MergedDictionaries` slot (`[0]` = color theme, `[1]` = text size); all `DynamicResource` bindings update live.

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

### Favorites

`IsFavorite` (bool) lives on `PhotoItem` (model) and `PhotoItemViewModel`. Toggling is a user gesture — never inferred from metadata.

Context menu: "Add to Favorites" (tag `Favorite`) shown when `IsFavorite=False`; "Remove from Favorites" (tag `RemoveFavorite`) shown when `IsFavorite=True`. `PhotoTile_ContextMenuOpening` applies multi-select visibility logic (same pattern as Mirror). `PhotoFavorite_Click` calls `OrganiseViewModel.ToggleFavorites(selected)`, which pushes a photo undo snapshot and saves the sidecar. `OnIsFavoriteChanged` resets `_toolTipLoaded = false` so "Favorite: Yes" appears on next hover.

Persistence: stored in `_photoorder.json` under a `"favorites"` key (list of filenames); omitted entirely when no items are favorited. `PhotoOrderSidecar.Favorites` is `List<string>?`.

**Favorites Only present mode**: `MainViewModel.FavoritesOnly` (bool, bound to a `CheckBox` in the toolbar). When true, `SwitchToPresent` builds a compacted list: for each folder whose photos contains at least one `IsFavorite && !IsRemoved` item, it creates a `List<PhotoItemViewModel>` of only those items. The full folder list and this parallel `effectivePhotos` list are passed to `PresentVM.SetFolders`. `PresentViewModel` stores `effectivePhotos` as `_effectivePhotoLists` (a `List<IReadOnlyList<PhotoItemViewModel>>`); `CurrentPhotos`, cumulative counts, and preload all read from `_effectivePhotoLists[folderIndex]` rather than `folder.Photos`. This decouples navigation from folder VM structure — folder VMs are never modified.

`OrganiseViewModel.GetAllFavorites()` returns all non-removed favorites across all non-removed folders (used by Export Favorites).

### Image adjustments

`Brightness` and `Contrast` (both `int`, range −100 to +100, default 0) live on `PhotoItem` and `PhotoItemViewModel`. Adjustments are non-destructive: never baked into stored bitmaps or files.

`PhotoItemViewModel.LoadBitmap(path, decodeWidth, brightness, contrast)` routes through `ApplyAdjustments(BitmapSource, int, int)` at the end of both the HEIC and standard decode branches. `ApplyAdjustments` early-returns the source unchanged when both values are 0 (zero cost for the common case). Otherwise it converts to `PixelFormats.Bgra32`, copies pixels into a `byte[]`, applies `v = (v − 128) × (1 + contrast/100.0) + 128 + brightness/100.0 × 255` per BGR channel clamped to [0, 255], writes into a `WriteableBitmap`, and `Freeze()`s it.

`LoadThumbnailAsync` bypasses `ThumbnailCache` entirely when `Brightness != 0 || Contrast != 0` — the cache key does not include adjustment values, so adjusted thumbnails are always recomputed to avoid stale or incorrectly-adjusted cached images. `OnBrightnessChanged` / `OnContrastChanged` sync the model and call `ReloadThumbnail()` (which calls `LoadThumbnailAsync` directly, bypassing the `_thumbnailRequested` guard in `EnsureThumbnailLoaded`). Both calls also reset `_toolTipLoaded`.

`OrganiseViewModel.SetAdjustments(photos, brightness, contrast)` pushes a photo undo snapshot then sets both values on every item and saves the sidecar. `PhotoTile_ContextMenuOpening` hides the Adjust menu item when every selected item is a video. `ImageAdjustmentDialog` (`Views/ImageAdjustmentDialog.xaml`) decodes a 300 px preview bitmap once on open, then calls `ApplyAdjustments` live on every slider change (cheap in-memory op, no disk re-read).

Persistence: `"adjustments"` dict in `_photoorder.json`; the key is omitted when no items have non-zero adjustments.

### Autoplay

`PresentView.xaml.cs` owns a `DispatcherTimer _autoplayTimer`. `UpdateAutoplayTimerState()` is called on every `IsAutoplayEnabled` or `AutoplayIntervalSeconds` property change: it starts the timer (with interval from `Vm.AutoplayIntervalSeconds`) when `IsAutoplayEnabled && !CurrentIsVideo`, and stops it otherwise. On `Tick`, it calls `Vm.NextPhoto()`. Autoplay stops automatically when a video is reached (the `CurrentIsVideo` change triggers `UpdateAutoplayTimerState`). A green "▶ Autoplay (P to stop)" overlay (top-right corner) is shown via `DataTrigger` on `IsAutoplayEnabled`; hidden when viewing a video.

`IsAutoplayEnabled` is not persisted (resets to false on every app launch). `AutoplayIntervalSeconds` is persisted via `UserSettings.AutoplayIntervalSeconds` (default 5). `P` key toggles `pvm.IsAutoplayEnabled` in `MainWindow.HandleKeyDown`.

### Fade transitions

When `IsFadeEnabled` is true, `PresentView.xaml.cs` begins a 250 ms `DoubleAnimation` on `PhotoImage.OpacityProperty` (0 → 1) immediately after a new photo bitmap is assigned. Not applied to videos (the `MediaElement` is never faded). `IsFadeEnabled` is bound two-way to the **Fade Transitions** checkbox in `SettingsWindow`; persisted as `UserSettings.FadeTransitionEnabled`.

### Settings window

`SettingsWindow` (`Views/SettingsWindow.xaml`) is a modal dialog (same pattern as `AboutWindow`) opened from the **Settings…** toolbar button. Its constructor receives `MainViewModel` and sets it as `DataContext`, enabling direct binding to `PresentVM.IsFadeEnabled` and `PresentVM.AutoplayIntervalSeconds`. `InitThemeComboBox()` / `InitTextComboBox()` set initial selections before wiring `SelectionChanged`, preventing spurious saves. Theme and Text Size changes are saved immediately; Fade and Autoplay are saved by `MainWindow.Window_Closing` reading from the bound VM properties.

### Keyboard shortcuts dialog

`ShortcutsWindow` (`Views/ShortcutsWindow.xaml`) is a parameterless modal listing all key bindings for both modes in two headed sections. Opened by the `?` key handler in `MainWindow.HandleKeyDown` — the check runs before the `CurrentMode != Present` early-return guard, so `?` works in both modes. Static content only; no data binding.

### Export Favorites

**`ExportFavorites_Click`** in `MainWindow.xaml.cs` uses `ShellFolderPicker.PickFolder` (a custom `IFileOpenDialog` COM wrapper that omits `FOS_FORCEFILESYSTEM`) so both local PC folders and MTP devices (Android phones connected via USB) appear in the destination picker. Returns `(string? fsPath, IShellItem? shellItem)` — exactly one is non-null. The two destination types follow separate paths:

**Filesystem destination** (PC folder): `ExportFavorites_Click` computes `toDelete` via `Directory.GetFiles(destFolder)` filtered against the favorites set, then shows `ExportDeleteConfirmDialog` if non-favorites exist. The dialog offers **Delete & Export** / **Export Only** / **Cancel**. `ExportProgressDialog(favorites, destFolder, toDelete?)` then runs a mirror copy: phase 1 deletes the `toDelete` list (if the user chose Delete & Export), phase 2 syncs favorites by comparing source vs. destination byte size — matching size → skip, different/missing → `File.Copy(overwrite: true)`, phase 3 writes `_presentation.json`.

**MTP destination** (phone via USB): `ExportProgressDialog(favorites, destShellItem)` opens directly (no delete-confirmation dialog — enumeration happens inside the progress dialog to keep the main window responsive). `StartMtpExportAsync` runs four phases: (0) **Scan** the phone folder via `ShellFileOperation.EnumerateFolderContents`, which binds `IShellItem → IShellFolder` via `BindToHandler(BHID_SFObject)`, calls `IShellFolder.EnumObjects → IEnumIDList`, and for each child PIDL calls `SHCreateItemWithParent → IShellItem2.GetDisplayName(SIGDN_PARENTRELATIVEPARSING)` + `IShellItem2.GetUInt64(PKEY_Size)` to build a `{filename → (size, IShellItem)}` dictionary; (1) **Delete** non-favorites via `IFileOperation.DeleteItem` (silently, one operation per file); (2) **Copy** new and size-changed files via `IFileOperation.CopyItem`; (3) **Write manifest** via a temp file + `IFileOperation.MoveItem`. Unchanged files (size matches) are skipped throughout. A summary line in the progress dialog reports how many files were skipped.

**COM infrastructure** (`Services/`):
- `ShellInterop.cs` — all COM/P/Invoke declarations: `IShellItem` (callable `BindToHandler`), `IShellItem2` (`GetUInt64` for `PKEY_Size`), `IShellFolder`, `IEnumIDList`, `IFileOpenDialog`, `IFileOperation`; `PROPERTYKEY` struct; `SHCreateItemFromParsingName` and `SHCreateItemWithParent` P/Invoke; constants `FOS_FORCEFILESYSTEM`, `BHID_SFObject`, `PKEY_Size`, `SHCONTF_NONFOLDERS`, `SIGDN_PARENTRELATIVEPARSING`
- `ShellFolderPicker.cs` — `PickFolder(nint ownerHwnd, string title)` → `(string? fsPath, IShellItem? shellItem)`
- `ShellFileOperation.cs` — `CopyFile`, `WriteTextFile` (temp + MoveItem), `DeleteShellItem`, `EnumerateFolderContents`, `GetDisplayName`

**`ExportDeleteConfirmDialog`** (`Views/ExportDeleteConfirmDialog.xaml`) is shown only for filesystem destinations when non-favorite files exist. It lists those filenames in a scrollable `ListBox` and offers three buttons: **Delete & Export** (`Choice = DeleteAndExport`), **Export Only** (`Choice = ExportOnly`), **Cancel** (`Choice = Cancel`).

`PresentationManifest` (`Models/PresentationManifest.cs`) is serialized with `System.Text.Json`. Each item carries a `"file"` key (filename only) and an optional `"caption"` key (omitted when null via `JsonIgnore(WhenWritingNull)`). This manifest is consumed by the Android companion app.

## Android companion app

`PhotoPresenterAndroid/` is a .NET MAUI Android app (`net9.0-android`, API 34+) that presents exported favorites on a phone or tablet.

### Architecture

| Layer | Location | Role |
|-------|----------|------|
| Models | `Models/` | `PresentationManifest` / `PresentationManifestItem` (mirrors Windows model, deserialization only); `MediaItem` record `(FullPath, Caption, IsVideo)` |
| Services | `Services/ManifestService.cs` | Reads `_presentation.json`, checks file existence, filters unsupported video formats (AVI/WMV/MKV), returns `List<MediaItem>` |
| Pages | `Pages/MainPage` | Folder picker (via `CommunityToolkit.Maui.Storage.FolderPicker`), last-folder persistence via `Preferences`, manifest check, navigation to `BrowsePage` |
| Pages | `Pages/BrowsePage` | 3-column thumbnail grid; `ThumbnailItem` private class with `INotifyPropertyChanged` for live thumbnail updates; `TapGestureRecognizer` on each tile drives navigation; `ActivityIndicator` spinner while thumbnails load; session persistence via `Preferences` (last-presented file path); in-memory thumbnail cache eliminates scroll-back reloads (see below) |
| Pages | `Pages/PresentPage` | Fullscreen presenter: `Image` for photos, `MediaElement` for video; Android-native gesture handling; autoplay timer; passes current index back to `BrowsePage` on back navigation |

### Gesture handling

MAUI's `PanGestureRecognizer` and `PinchGestureRecognizer` conflict on Android (two-finger spread is consumed by the pan recognizer before pinch can start). The solution bypasses MAUI's gesture layer and attaches Android's native `ScaleGestureDetector` + `GestureDetector` directly to the native view of a transparent `BoxView` overlay (`GestureOverlay`). The overlay sits above `Image`/`MediaElement` in Z-order (so it intercepts all touch) but below the `Button` controls (so Back/Autoplay remain tappable).

- `ScaleGestureDetector` (`PinchListener`) — handles two-finger pinch; scale clamped [1×, 5×]; `_wasScaling` flag set on `OnScaleBegin` to suppress the spurious fling that `GestureDetector` fires when fingers lift after a pinch
- `GestureDetector` (`FlingScrollListener`) — implements both `IOnGestureListener` and `IOnDoubleTapListener`; wired with `_gestureDetector.SetOnDoubleTapListener(flingListener)` so double-tap events reach the same instance:
  - `OnScroll` — pans when `_scale > 1.05` (converts px→DIPs by dividing by `DisplayMetrics.Density`)
  - `OnFling` — navigates next/prev at any zoom level when `|velocityX| > 300 px/s`; suppressed for one event after a pinch via `_wasScaling`
  - `OnDoubleTap` — resets zoom and pan to 1× via `ResetZoomPan()`
  - `OnSingleTapConfirmed` — toggles `CaptionBorder.IsVisible` for items that have a caption (fires ~300 ms after tap so Android can rule out a double-tap first)

### Navigation

`AppShell` registers both `BrowsePage` and `PresentPage` via `Routing.RegisterRoute`. Navigation flow:

```
MainPage ──[Open Presentation]──▶ BrowsePage ──[tap thumbnail]──▶ PresentPage
                                       ▲                                │
                                       └──────────[Back]────────────────┘
```

`MainPage` navigates to `BrowsePage` passing `List<MediaItem>` as `"Items"`. `BrowsePage` navigates to `PresentPage` passing `"Items"` + `"StartIndex"` (int, 0-based). `PresentPage.BackButton_Clicked` uses `GoToAsync("..", {"LastIndex": _index})` — MAUI Shell calls `IQueryAttributable.ApplyQueryAttributes` on the revealed `BrowsePage` with these params, updating `_index` and re-scrolling to the correct tile. All three pages implement `IQueryAttributable`.

`BrowsePage` session persistence: on forward navigation from `MainPage`, it reads `Preferences.Default.Get("LastPresentedFile", "")` and finds the matching index in the item list; the `"last item" → restart` rule (if `idx == items.Count - 1`, treat as finished and start from 0) prevents the browse screen from always highlighting the very last item after a completed presentation. `PresentPage.ShowItem` writes the current file path to `Preferences` on every navigation.

### Browse thumbnail caching

`CollectionView` virtualizes off-screen items — when MAUI recycles a cell, it re-requests the image from the `ImageSource`. `ImageSource.FromFile(fullPath)` causes Glide to re-decode the full-resolution photo (potentially 10+ MB) each time a tile scrolls back into view, producing visible reload flicker.

The fix: `LoadAllThumbnailsAsync` pre-decodes every thumbnail to display size on background threads and stores the result as a `byte[]` inside `ThumbnailItem`. `SetThumbnailBytes(byte[])` creates `ImageSource.FromStream(() => new MemoryStream(_bytes))` once and stores it. On scroll-back the stream factory runs again, but only reads from the existing in-memory `byte[]` — no disk I/O, no full-resolution decode.

Photo decoding uses `BitmapFactory` with a two-pass approach: first `InJustDecodeBounds = true` to read dimensions without decoding pixels, then a second decode with `InSampleSize` set to the largest power-of-two that keeps the output at or above the target width (`DisplayMetrics.WidthPixels / 3`). Video thumbnails use `MediaMetadataRetriever.GetFrameAtTime`. Both are compressed to JPEG at 70–80% quality before storage. A `SemaphoreSlim(4)` caps concurrent workers; tiles populate progressively as each worker completes.

Memory cost is approximately 30–60 KB per item (JPEG bytes) — about 5 MB for 100 items, well within the budget of a modern phone.

### Key packages

- `CommunityToolkit.Maui` — `FolderPicker`
- `CommunityToolkit.Maui.MediaElement` — ExoPlayer-backed video playback
- `Xamarin.AndroidX.LocalBroadcastManager` — required transitive dependency of `MediaElement` (not auto-included in debug builds; must be an explicit package reference)
- `EmbedAssembliesIntoApk=true` in the csproj — bundles managed DLLs in the APK, preventing the "No assemblies found in FastDev directory" crash that occurs when the app is closed and restarted outside of a live-deploy session

## Tests

```powershell
dotnet test PhotoPresenter.Tests/PhotoPresenter.Tests.csproj
```

Test project at `PhotoPresenter.Tests/` targets `net8.0-windows10.0.19041.0` with `UseWPF=true`, `OutputType=Library`. `AssemblyInfo.cs` carries `[InternalsVisibleTo("PhotoPresenter.Tests")]` so all `internal` members are visible to tests.

### Structure

363 tests as of the last full run (`dotnet test` output: `Passed! - Failed: 0, Passed: 363`).

```
Unit/
  FileClassificationTests.cs       — IsMediaFile, IsVideoFile
  SidecarParsingTests.cs           — ApplyFolderOrder, LoadPhotosForFolder (uses TempDirectory)
  OrganiseViewModelTests.cs        — Reorder, Remove/Restore, Undo, SetCaption/SetCaptions, ToggleMirrors,
                                      ToggleFavorites, GetAllFavorites, SetAdjustments,
                                      SortFoldersByName, SortPhotosByDateAsync, FolderCountLabel,
                                      PhotoCountLabel, CurrentFolderItems/CurrentPhotoItems ShowAll filtering
                                      (mocked IPhotoLibraryService; LoadAsync(@"Z:\nonexistent") skips FSW)
  ExifOrientationTests.cs          — ApplyExifOrientation ([StaTheory]), ReadOrientationFromMetadata
  PhotoFolderViewModelTests.cs     — FolderToolTipText, UpdatePath cascade, constructor photo separation
  PhotoItemViewModelTests.cs       — HasThumbnail, EnsureThumbnailLoaded idempotency,
                                      RetryThumbnailAfterDelayAsync early-exit and pending-check, UpdatePath,
                                      Brightness/Contrast model sync and thumbnail reload, ApplyAdjustments
  PresentViewModelTests.cs         — OverallLabel format, cumulative-count correctness, start-position clamping,
                                      NextPhoto/PreviousPhoto navigation (incl. folder-boundary wrap),
                                      effectivePhotos/FavoritesOnly navigation and label correctness,
                                      ZoomIn/ZoomOut/ZoomByDelta bounds, BeginPan/UpdatePan, RotateVideo,
                                      MirrorScaleX, HasCurrentCaption, PlayPauseIcon, UpdatePosition time format,
                                      CurrentFolder/CurrentPhotoItem, SetFolders resets, AutoplayIntervalSeconds
  TextUtilsTests.cs                — NormalizeCaption
  CrossFolderMoveTests.cs          — MovePhotosToFolder reorder and undo
  MainWindowKeyTests.cs            — HandleKeyDown dispatch for all key bindings
  MainWindowTests.cs               — MainWindow construction and mode switching
  FswHandlerTests.cs               — FileSystemWatcher Created/Deleted/Renamed handler logic
Integration/
  SidecarRoundTripTests.cs      — real temp folders, save→load round-trips (incl. favorites, adjustments)
  LibraryLoadTests.cs           — LoadLibraryAsync scenarios
  UserSettingsTests.cs          — Load/Save with temp path, AutoplayIntervalSeconds default and round-trip
  OrganiseSyncTests.cs          — reconcile folder contents on selection
  ThumbnailCacheTests.cs        — cache key construction, hit/miss, pruning
Infrastructure/
  StaFactAttribute.cs           — [StaFact] runs test on STA thread (required for WPF imaging types)
  StaTheoryAttribute.cs         — [StaTheory] parameterized STA tests
  StaTestCase.cs                — STA thread runner
  TempDirectory.cs              — IDisposable temp folder; TinyJpegBytes = minimal valid 1×1 JPEG
  WpfApplicationFixture.cs      — ensures a WPF Application instance exists for tests that need it
```

### Internal members exposed for testing

| Member | File | Promoted for |
|--------|------|--------------|
| `ApplyFolderOrder` / `LoadPhotosForFolder` | `PhotoLibraryService.cs` | Sidecar parsing tests |
| `ReadOrientationFromMetadata` / `ApplyExifOrientation` | `PhotoItemViewModel.cs` | EXIF orientation tests |
| `ApplyAdjustments` | `PhotoItemViewModel.cs` | Brightness/contrast pixel-transform tests |
| `Load(path)` / `Save(path)` overloads | `UserSettings.cs` | Settings round-trip tests |
| `RetryThumbnailAfterDelayAsync()` | `PhotoItemViewModel.cs` | FSW thumbnail retry; awaitable so tests can exercise the loop without real delays |
| `ShowStatus(message)` | `OrganiseViewModel.cs` | Status banner tests; also called by `SwitchToPresent` on no-favorites abort |
| `HandleKeyDown(key, modifiers)` | `MainWindow.xaml.cs` | Key binding dispatch tests without needing real WPF key events |

### Key conventions

- STA thread (`[StaFact]` / `[StaTheory]`) required for any code that creates `BitmapSource`, `TransformedBitmap`, or `BitmapImage`
- `OrganiseViewModel` tests pass a non-existent path to `LoadAsync` so the `FileSystemWatcher` silently skips initialisation
- `TempDirectory` is `IDisposable` — use in a `using` block; `TinyJpegBytes` provides a minimal valid JPEG without needing STA

## Global usings

`GlobalUsings.cs` adds `System.IO`, `System.Windows.Media`, and `System.Windows.Media.Imaging` globally (required because the WPF SDK creates a temporary project for compilation that does not inherit all implicit usings).
