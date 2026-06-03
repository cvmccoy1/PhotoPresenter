# PhotoPresenter

A Windows WPF application for presenting family photo collections on a TV. Browse and organise your photos in one mode, then present them fullscreen in another.

## Features

### Organise Mode
- Browse a parent folder containing subfolders of photos
- Drag and drop subfolders to set a custom presentation order
- Select a subfolder and drag and drop its photos to reorder them
- Right-click a folder or photo and choose **Remove from Presentation** to exclude it — the actual files are never touched
- Tick **Show All** in either pane to reveal removed items at reduced opacity; right-click a grayed item and choose **Add to Presentation** to restore it
- Custom order and removed items are saved to small JSON sidecar files — your actual files and folders are never renamed or moved
- Click a folder (and optionally a photo) before switching to Present mode to start the slideshow from that point
- Window size and position are remembered between sessions

### Present Mode
- Fullscreen display on your TV or monitor
- Starts at the folder and photo selected in Organise mode
- Clean black background, photo fills the screen — no transitions, no effects
- Keyboard navigation:
  - **Right Arrow** or **Space** — next photo
  - **Left Arrow** — previous photo
  - Automatically moves between subfolders at boundaries
- Zoom and pan:
  - **+** / **-** — zoom in / out
  - **Scroll wheel** — zoom in / out
  - **Click and drag** — pan a zoomed image
  - **Escape** — return to Organise mode

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

If a sidecar file doesn't exist, folders are shown in alphabetical order and photos in creation-date order. Items not listed in a sidecar (e.g. newly added photos) are appended at the end automatically.

### Removing and restoring items
Removing a folder or photo from the presentation adds its name to the `removed` list in the relevant sidecar file. It will be excluded from Organise and Present modes on the next launch. Tick **Show All** to see removed items and restore them individually at any time.

### Supported formats
JPG, JPEG, PNG, BMP, GIF, TIFF, HEIC, HEIF

HEIC/HEIF requires the free [HEIF Image Extensions](https://apps.microsoft.com/detail/9pmmsr1cgpwg) from the Microsoft Store.

## Project Structure

```
PhotoPresenter/
├── Models/          Pure data models
├── Services/        File I/O, folder scanning, settings persistence
├── ViewModels/      MVVM logic (CommunityToolkit.Mvvm)
└── Views/           XAML UI (MainWindow, OrganiseView, PresentView)
```

Built with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet).
