# PhotoPresenter

A Windows WPF application for presenting family photo collections on a TV. Browse and organise your photos in one mode, then present them fullscreen in another.

## Features

### Organise Mode
- Browse a parent folder containing subfolders of photos
- Drag and drop subfolders to set a custom presentation order
- Select a subfolder and drag and drop its photos to reorder them
- Custom order is saved to small JSON sidecar files — your actual files and folders are never renamed or moved

### Present Mode
- Fullscreen display on your TV or monitor
- Clean black background, photo fills the screen — no transitions, no effects
- Keyboard navigation:
  - **Right Arrow** or **Space** — next photo
  - **Left Arrow** — previous photo
  - Automatically moves between subfolders at boundaries
- Zoom and pan:
  - **+** / **-** — zoom in / out
  - **Scroll wheel** — zoom in / out
  - **Right-click drag** — pan a zoomed image
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

### Folder order
When you reorder subfolders in Organise mode, the order is saved to `_photofolderorder.json` in the parent folder. If this file doesn't exist, subfolders are shown in alphabetical order.

### Photo order
When you reorder photos within a subfolder, the order is saved to `_photoorder.json` inside that subfolder. If this file doesn't exist, photos are shown in creation date order.

### Supported formats
JPG, JPEG, PNG, BMP, GIF, TIFF

## Project Structure

```
PhotoPresenter/
├── Models/          Pure data models
├── Services/        File I/O, folder scanning, settings persistence
├── ViewModels/      MVVM logic (CommunityToolkit.Mvvm)
└── Views/           XAML UI (MainWindow, OrganiseView, PresentView)
```

Built with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet).
