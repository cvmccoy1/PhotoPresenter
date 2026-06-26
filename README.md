# PhotoPresenter

A Windows WPF application for presenting family photo and video collections on a TV. Browse and organise your media in one mode, then present it fullscreen in another.

![PhotoPresenter Organise mode](docs/screenshot.png)

## Features

### Organise Mode
- Browse a parent folder containing subfolders of photos and videos
- Drag and drop subfolders to set a custom presentation order
- Click **Sort by Name** in the Folders pane to sort all folders alphabetically (A→Z); a confirmation is shown before the saved order is updated
- Select a subfolder and drag and drop its items to reorder them; drag items from one subfolder to another to move them to a different folder
- Click **Sort by Date** in the Photos/Videos pane to sort all items in the selected folder by creation date (oldest first); a confirmation is shown first
- Both panes support multi-selection: **Ctrl+Click** toggles an item, **Shift+Click** selects a range, **Ctrl+A** selects all; clicking empty space clears the selection
- Drag a selected item to move the entire selection together, preserving relative order
- **Delete** removes all selected items from the presentation at once
- Video files appear with a play-button thumbnail; the footer shows item counts as **x Items (y Photos, z Videos)**
- The Folders pane footer shows the folder count and aggregate photo, video, and total counts across all visible folders on a single line (e.g. **5 folders · 48 Photos, 7 Videos, 55 Total**)
- Right-click a folder or item and choose **Remove from Presentation** to exclude it — the actual files are never touched; with a multi-selection, the menu adapts: **Remove from Presentation** appears if any selected items are visible, **Add to Presentation** if any are hidden, or both if mixed — each action applies only to the applicable items
- Tick **Show All** in either pane to reveal removed items at reduced opacity; right-click a grayed item and choose **Add to Presentation** to restore it
- Right-click a folder and choose **Open Folder in Explorer** to open that folder in Windows File Explorer; the option is greyed out when multiple folders are selected
- Right-click any photo or video and choose **Open Folder in Explorer** to open its containing folder in Windows File Explorer; if one or more items are selected, those files are also pre-selected in Explorer
- Hover over a folder to see a tooltip showing the number of photos, videos, and total items in that folder
- Right-click any photo or video and choose **Open** to open it in its default app, or **Open With…** to choose another app; double-clicking a tile does the same as **Open**
- Hover over any photo or video tile to see a tooltip showing its type, date, dimensions (photos) or length (videos), and file size
- Right-click any photo or video and choose **Mirror** to horizontally flip it; right-click again and choose **Remove Mirror** to restore it; with a multi-selection, each item toggles independently — if states are mixed, both **Mirror** and **Remove Mirror** appear and apply only to the applicable items; the flip is shown on the thumbnail in Organise mode (photos) and applied during Present mode for both photos and videos; mirror state is saved to the sidecar and covered by undo
- Right-click any photo or video and choose **Add to Favorites** to mark it; right-click again to **Remove from Favorites**; with a multi-selection the menu applies the action to all applicable items; favorited items show a star (★) badge on their thumbnail; hover a tile to see "Favorite: Yes" in the tooltip; favorite state is saved to the sidecar and covered by undo
- Right-click any photo (not video) and choose **Adjust Brightness/Contrast…** to open a dialog with two sliders (−100 to +100); the preview updates live as you move the sliders; click **OK** to apply, **Reset** to zero both sliders, or **Cancel** to discard; adjustments appear on the thumbnail immediately, are saved to the sidecar, covered by undo, and applied automatically in Present mode
- Right-click any photo or video and choose **Add Caption** to attach a text caption; right-click again to **Edit Caption** or **Delete Caption**; with a multi-selection, these become **Set Caption** (writes the same caption to all selected items) and **Delete Caption** (removes captions from all that have one)
  - Press **Shift+Enter** in the caption dialog to insert a line break; the text box grows as lines are added
  - Multi-line captions are displayed centered in both Organise mode (below the thumbnail) and Present mode (overlaid at the bottom of the screen)
  - The caption dialog includes a built-in spellchecker — misspelled words are underlined in red; right-click a word to see correction suggestions
- **Ctrl+Z** or the **↩ Undo** toolbar button undoes the last action; up to 20 steps of history are kept — covers all reorders, removes, restores, sorts, mirror, favorites, captions, and brightness/contrast changes; a multi-select operation counts as one step; the history is cleared when a new folder is opened
- Thumbnails load on demand: folder-card thumbnails (left pane) load immediately on launch; photo thumbnails load when you click a folder, keeping startup fast even with thousands of files; decoded thumbnails are cached in `%LOCALAPPDATA%\PhotoPresenter\thumbcache\` so subsequent visits are near-instant
- Custom order and removed items are saved to small JSON sidecar files — your actual files and folders are never renamed or moved
- The app watches for external changes while it is running: if you rename a subfolder in Explorer, the new name appears immediately and the saved order is updated automatically; if a subfolder or photo is deleted externally, it is removed from the list and a brief notice appears; new photos copied into the selected folder appear automatically
- Click a folder (and optionally an item) before switching to Present mode to start from that point; press **Space**, **F5**, or click **▶ Present** to enter Present mode; when you exit Present mode the last photo or video shown becomes the selected item in Organise mode, so pressing **Space** or **F5** again picks up exactly where you left off
  - **Space** and **F5** are intercepted only when a list item or the window background has focus; if a toolbar button, checkbox, or dropdown has keyboard focus, Space activates that control as normal
- Tick **Favorites Only** (next to the ▶ Present button) to limit the presentation to favorited items only; the overall counter reflects only the favorites set; the presentation starts from the currently selected item if it is a favorite, or the first favorite otherwise
- Click **Export Favorites…** in the toolbar to copy all favorited photos and videos to a folder of your choice (flat copy, no subfolders, existing files skipped); if the destination already contains files that are not favorites, a confirmation dialog lists them and offers: **Delete & Export** (removes non-favorites then copies), **Export Only** (copies without deleting), or **Cancel**; the export also writes a `_presentation.json` manifest (item order + captions) that the Android companion app reads
- Window size, position, splitter position, selected folder, selected photo/video, both pane scroll positions, Show All checkbox states, and volume are all remembered between sessions
- Press **?** at any time to open a keyboard shortcuts reference
- Click **Settings…** in the toolbar to open the Settings window; changes to Theme and Text Size take effect immediately
  - **Theme**: Light (default), Dark, High Contrast Light, High Contrast Dark, Slate Blue, Forest, Sunset, Amethyst, Teal
  - **Text Size**: Small, Normal (default), Large, Extra Large — applies to any theme
  - **Autoplay Interval**: how long each photo is shown during autoplay in Present mode — 2, 3, 5 (default), 10, 15, 20, or 30 seconds
  - **Fade Transitions**: smooth 250 ms opacity fade when advancing to the next photo in Present mode (enabled by default; not applied to videos)

### Present Mode
- Fullscreen display on your TV or monitor
- Starts at the folder and item selected in Organise mode; tick **Favorites Only** in the toolbar before entering to limit the presentation to favorited items
- Clean black background, media fills the screen
- Keyboard navigation:
  - **F5** — enter Present mode (also works from Organise mode)
  - **Right Arrow** — next item
  - **Space** — next photo; toggle play/pause for video
  - **Left Arrow** — previous item
  - Automatically moves between subfolders at boundaries
  - **Escape** — return to Organise mode; the last item shown becomes the active selection so the next presentation resumes from that point
  - **P** — toggle autoplay; each photo advances automatically after the configured interval (set in Settings); autoplay stops when a video is reached; a green **▶ Autoplay (P to stop)** indicator appears in the top-right corner while active
  - **?** — open keyboard shortcuts reference
- Photo zoom and pan:
  - **+** / **-** — zoom in / out
  - **Scroll wheel** — zoom in / out
  - **Click and drag** — pan a zoomed image
- Overall counter — shows the global position of the current item across all folders (e.g. **759 of 1956**); displayed alongside the per-folder position for both photos and videos; in Favorites Only mode the counter reflects only the favorites set
- Video playback controls (shown at the bottom of the screen):
  - Scrub slider — click anywhere on the track to jump to that position; drag the thumb for precise live scrubbing (the video frame updates as you drag)
  - **↺** — restart from the beginning
  - **▶ / ⏸** — play / pause
  - Position display — current time and total duration (e.g. 0:23 / 3:16)
  - **↻ 90°** — rotate the video 90° clockwise; videos are auto-rotated on load using the orientation metadata written by phone cameras, so portrait videos display upright automatically
  - Volume slider
- If a video's audio codec is not supported by the Windows media infrastructure, the video still plays automatically and an amber banner appears at the top of the screen explaining that audio is unavailable; navigating to another item clears the banner

## Android Companion App

`PhotoPresenterAndroid` is a .NET MAUI Android app that presents your exported favorites on a phone or tablet connected to a TV.

### Workflow

1. **Export on PC** — Click **Export Favorites…** and choose a destination folder. The app copies the favorited media and writes a `_presentation.json` manifest.
2. **Transfer to phone** — Copy the export folder to the Android device via USB.
3. **Present** — Open Photo Presenter on Android, pick the folder, and tap **Open Presentation**.

### Android app features

- Fullscreen black-background display of photos and videos in manifest order
- **Swipe left/right** to navigate to the next/previous item (works at any zoom level)
- **Pinch to zoom** (1×–5×); snaps back to 1× if you end the gesture below 1.1×
- **Drag to pan** a zoomed photo or video
- Caption overlay at the bottom of the screen when a caption is set
- Video playback with ExoPlayer (MP4, MOV, M4V); auto-advances to the next item when the video ends
- **Auto ▶** button for timed autoplay (5-second interval); pauses automatically on videos
- Item counter in the top-right corner (e.g. **3 / 47**)
- Remembers the last folder between sessions

### Android requirements

- Android 14 (API 34) or later
- .NET MAUI workload (`dotnet workload install maui-android`)

### Building the Android app

```powershell
dotnet build PhotoPresenterAndroid/PhotoPresenterAndroid.csproj
```

Or open `PhotoPresenter.sln` in Visual Studio 2022 (requires the **Mobile development with .NET** workload) and deploy to a device or emulator.

## Requirements

- Windows 10 or 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Desktop Runtime)

## Building from Source

Visual Studio 2022 with the **.NET desktop development** workload installed.

```
git clone https://github.com/cvmccoy1/PhotoPresenter.git
cd PhotoPresenter
dotnet build
```

Or open `PhotoPresenter.sln` directly in Visual Studio 2022 and press **F5**.

## How It Works

### Folder and photo order
When you reorder subfolders or photos in Organise mode, the order is saved to sidecar files:
- `_photofolderorder.json` in the parent folder — stores folder order and any removed folders
- `_photoorder.json` in each subfolder — stores photo order and any removed photos

If a sidecar file doesn't exist, folders are shown in alphabetical order and photos in date order. Items not listed in a sidecar (e.g. newly added photos) are appended at the end automatically.

### Presentation manifest
`_presentation.json` is written to the export folder by **Export Favorites…**. It lists every exported item in order and includes any captions:

```json
{
  "version": 1,
  "items": [
    { "file": "img001.jpg", "caption": "Summer 2024" },
    { "file": "clip.mp4" }
  ]
}
```

Items without a caption omit the `"caption"` key entirely. The Android companion app reads this file to know the presentation order and captions; items whose files are missing are silently skipped, and video formats not supported by Android (AVI, WMV, MKV) are filtered out automatically.

### Captions
Captions are stored in each subfolder's `_photoorder.json` sidecar under a `captions` key. They are never written to the image files themselves.

### Favorites
Favorites are stored in each subfolder's `_photoorder.json` sidecar under a `favorites` key (a list of filenames). The key is omitted entirely when no items in that folder are marked as favorites.

### Image adjustments
Brightness and contrast values are stored in each subfolder's `_photoorder.json` sidecar under an `adjustments` key (a dictionary mapping filename to `{ brightness, contrast }`). The key is omitted when no items have non-zero adjustments. Adjustments are applied non-destructively at render time — the original files are never modified.

### Removing and restoring items
Removing a folder or photo from the presentation adds its name to the `removed` list in the relevant sidecar file. It will be excluded from Organise and Present modes on the next launch. Tick **Show All** to see removed items and restore them individually at any time.

### External changes while the app is running
The app uses `FileSystemWatcher` to detect changes made outside the app while it is open:

| Change | What happens |
|--------|-------------|
| Subfolder renamed | Name updates live in the Folders pane; saved order is updated to match |
| Subfolder deleted | Folder disappears from the list; a brief notice appears at the top of the screen |
| New subfolder created | Appears at the bottom of the Folders pane |
| Photo or video added to the selected folder | Tile appears automatically; thumbnail updates within seconds even if Windows Shell hasn't caught up yet |
| Photo or video deleted from the selected folder | Tile disappears |
| Photo or video renamed in the selected folder | Filename updates in place |

### Supported formats
**Photos:** JPG, JPEG, PNG, BMP, GIF, TIFF, HEIC, HEIF

HEIC/HEIF requires the free [HEIF Image Extensions](https://apps.microsoft.com/detail/9pmmsr1cgpwg) from the Microsoft Store. HEIF also depends on an HEVC codec, which Windows Update typically delivers silently over time — so most PCs already have it without the user ever noticing. If HEIC images show no thumbnail or fail to load, search the Microsoft Store for **HEVC Video Extensions from Device Manufacturer** (free for most Intel/AMD devices) or the [HEVC Video Extensions](https://apps.microsoft.com/detail/9nmzlz57r3t7) (~$0.99 for uncovered devices).

**Videos:** MOV, MP4, AVI, WMV, M4V, MKV

Video playback uses the Windows media infrastructure. Most common formats work out of the box on Windows 10/11; some codecs (e.g. HEVC/H.265) may require the free [HEVC Video Extensions](https://apps.microsoft.com/detail/9nmzlz57r3t7) from the Microsoft Store.

## Project Structure

```
PhotoPresenter/              Windows WPF app
├── Models/                  Pure data models
├── Services/                File I/O, folder scanning, settings persistence
├── ViewModels/              MVVM logic (CommunityToolkit.Mvvm)
└── Views/                   XAML UI (MainWindow, OrganiseView, PresentView, dialogs)

PhotoPresenterAndroid/       Android companion app (.NET MAUI)
├── Models/                  PresentationManifest, MediaItem
├── Services/                ManifestService — reads _presentation.json
├── Pages/                   MainPage (folder picker), PresentPage (fullscreen viewer)
└── Platforms/Android/       MainActivity, MainApplication, AndroidManifest.xml

PhotoPresenter.Tests/        xUnit test suite (353+ tests)
```

Built with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) and [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui).
