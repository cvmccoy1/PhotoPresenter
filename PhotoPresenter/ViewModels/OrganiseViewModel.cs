using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Models;
using PhotoPresenter.Services;

namespace PhotoPresenter.ViewModels;

public partial class OrganiseViewModel : ObservableObject, IDisposable
{
    private readonly IPhotoLibraryService _library;

    private FileSystemWatcher? _parentWatcher;
    private FileSystemWatcher? _folderWatcher;
    private DispatcherTimer?   _statusTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    public bool HasStatusMessage => StatusMessage != null;

    // Parallel collection that contains ALL folders (active first, removed at end).
    private readonly ObservableCollection<PhotoFolderViewModel> _allFolderItems = new();

    public string ParentFolderPath { get; private set; } = "";

    // Active folders only — used for D&D reordering and Present mode.
    public ObservableCollection<PhotoFolderViewModel> Folders { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Photos))]
    [NotifyPropertyChangedFor(nameof(CurrentPhotoItems))]
    [NotifyPropertyChangedFor(nameof(PhotoCountLabel))]
    private PhotoFolderViewModel? _selectedFolder;

    [ObservableProperty]
    private PhotoItemViewModel? _selectedPhoto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFolderItems))]
    [NotifyPropertyChangedFor(nameof(FolderCountLabel))]
    private bool _showAllFolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPhotoItems))]
    [NotifyPropertyChangedFor(nameof(PhotoCountLabel))]
    private bool _showAllPhotos;

    // Switches between active-only Folders and all-items list.
    public ObservableCollection<PhotoFolderViewModel> CurrentFolderItems =>
        ShowAllFolders ? _allFolderItems : Folders;

    public string FolderCountLabel
    {
        get
        {
            int active  = Folders.Count;
            int removed = _allFolderItems.Count - active;
            int photos = 0, videos = 0;
            foreach (var f in Folders)
                foreach (var p in f.Photos)
                    if (p.IsVideo) videos++; else photos++;
            int total = photos + videos;

            string folderPart = ShowAllFolders && removed > 0
                ? $"{active + removed} folders  ({removed} removed)"
                : $"{active} folder{(active == 1 ? "" : "s")}";

            return $"{folderPart}  ·  {photos} Photo{(photos == 1 ? "" : "s")}, {videos} Video{(videos == 1 ? "" : "s")}, {total} Total";
        }
    }

    // Kept for backward compat (PresentViewModel reads SelectedFolder.Photos directly).
    public ObservableCollection<PhotoItemViewModel>? Photos => SelectedFolder?.Photos;

    // Switches between active-only and all-items photo list for the selected folder.
    public ObservableCollection<PhotoItemViewModel>? CurrentPhotoItems =>
        ShowAllPhotos ? SelectedFolder?.AllPhotoItems : SelectedFolder?.Photos;

    public string PhotoCountLabel
    {
        get
        {
            if (SelectedFolder == null) return "";
            int photoCount = 0, videoCount = 0;
            foreach (var p in SelectedFolder.Photos)
                if (p.IsVideo) videoCount++; else photoCount++;
            int total = photoCount + videoCount;
            int removed    = SelectedFolder.AllPhotoItems.Count - total;

            string baseLabel;
            if (photoCount > 0 && videoCount > 0)
                baseLabel = $"{total} Items ({photoCount} Photo{(photoCount == 1 ? "" : "s")}, {videoCount} Video{(videoCount == 1 ? "" : "s")})";
            else if (videoCount > 0)
                baseLabel = $"{videoCount} Video{(videoCount == 1 ? "" : "s")}";
            else
                baseLabel = $"{photoCount} Photo{(photoCount == 1 ? "" : "s")}";

            if (ShowAllPhotos && removed > 0)
                return $"{baseLabel}  ({removed} removed)";
            return baseLabel;
        }
    }

    // ── Undo history ───────────────────────────────────────────────────────────

    private const int MaxUndoDepth          = 20;
    private const int FswDebounceMs         = 150; // wait for OS to finish write/create
    private const int StatusBannerDurationSec = 5;

    private sealed record FolderSnapshot(
        IReadOnlyList<(PhotoFolderViewModel Vm, bool IsRemoved)> Items);

    private sealed record PhotoSnapshot(
        PhotoFolderViewModel Folder,
        IReadOnlyList<(PhotoItemViewModel Vm, bool IsRemoved, string Caption, bool IsMirrored)> Items);

    private readonly List<object> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    private void PushFolderUndo()
    {
        _undoStack.Add(new FolderSnapshot(
            _allFolderItems.Select(f => (f, f.IsRemoved)).ToArray()));
        if (_undoStack.Count > MaxUndoDepth) _undoStack.RemoveAt(0);
        OnPropertyChanged(nameof(CanUndo));
    }

    private void PushPhotoUndo(PhotoFolderViewModel folder)
    {
        _undoStack.Add(new PhotoSnapshot(folder,
            folder.AllPhotoItems.Select(p => (p, p.IsRemoved, p.Caption, p.IsMirrored)).ToArray()));
        if (_undoStack.Count > MaxUndoDepth) _undoStack.RemoveAt(0);
        OnPropertyChanged(nameof(CanUndo));
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var entry = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        if (entry is FolderSnapshot fs)
            ApplyFolderSnapshot(fs);
        else if (entry is PhotoSnapshot ps)
            ApplyPhotoSnapshot(ps);

        OnPropertyChanged(nameof(CanUndo));
    }

    private void ApplyFolderSnapshot(FolderSnapshot snap)
    {
        Folders.Clear();
        _allFolderItems.Clear();

        foreach (var (vm, wasRemoved) in snap.Items)
        {
            vm.IsRemoved = wasRemoved;
            _allFolderItems.Add(vm);
            if (!wasRemoved) Folders.Add(vm);
        }

        SaveAllFolderOrder();
        OnPropertyChanged(nameof(FolderCountLabel));

        if (SelectedFolder == null || !Folders.Contains(SelectedFolder))
            SelectedFolder = Folders.FirstOrDefault();
    }

    private void ApplyPhotoSnapshot(PhotoSnapshot snap)
    {
        var folder = snap.Folder;

        folder.Photos.Clear();
        folder.AllPhotoItems.Clear();

        foreach (var (vm, wasRemoved, caption, wasMirrored) in snap.Items)
        {
            vm.IsRemoved  = wasRemoved;
            vm.Caption    = caption;
            vm.IsMirrored = wasMirrored;
            folder.AllPhotoItems.Add(vm);
            if (!wasRemoved) folder.Photos.Add(vm);
        }

        SaveAllPhotoOrder(folder);
        OnPropertyChanged(nameof(PhotoCountLabel));

        if (SelectedPhoto == null || !folder.Photos.Contains(SelectedPhoto))
            SelectedPhoto = folder.Photos.FirstOrDefault();
    }

    // ── Constructor / Load ─────────────────────────────────────────────────────

    public OrganiseViewModel(IPhotoLibraryService library)
    {
        _library = library;
    }

    public void Dispose()
    {
        _parentWatcher?.Dispose();
        _folderWatcher?.Dispose();
        _statusTimer?.Stop();
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(StatusBannerDurationSec) };
        _statusTimer.Tick += (_, _) => { StatusMessage = null; _statusTimer.Stop(); };
        _statusTimer.Start();
    }

    private void StartParentWatcher(string parentPath)
    {
        _parentWatcher?.Dispose();
        if (!Directory.Exists(parentPath)) return;
        _parentWatcher = new FileSystemWatcher(parentPath)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            Filter = "*",
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _parentWatcher.Renamed += OnSubfolderRenamed;
        _parentWatcher.Deleted += OnSubfolderDeleted;
        _parentWatcher.Created += OnSubfolderCreated;
    }

    private void UpdateFolderWatcher(string? folderPath)
    {
        _folderWatcher?.Dispose();
        _folderWatcher = null;
        if (folderPath == null || !Directory.Exists(folderPath)) return;
        _folderWatcher = new FileSystemWatcher(folderPath)
        {
            NotifyFilter = NotifyFilters.FileName,
            Filter = "*",
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _folderWatcher.Created += OnFolderFileCreated;
        _folderWatcher.Deleted += OnFolderFileDeleted;
        _folderWatcher.Renamed += OnFolderFileRenamed;
    }

    // ── FileSystemWatcher event handlers ───────────────────────────────────────

    private void OnSubfolderRenamed(object sender, RenamedEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var vm = _allFolderItems.FirstOrDefault(f => f.FullPath == e.OldFullPath);
            if (vm == null) return;
            vm.UpdatePath(e.Name ?? Path.GetFileName(e.FullPath), e.FullPath);
            if (SelectedFolder == vm)
                UpdateFolderWatcher(e.FullPath);
            SaveAllFolderOrder();
        });
    }

    private void OnSubfolderDeleted(object sender, FileSystemEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var vm = _allFolderItems.FirstOrDefault(f => f.FullPath == e.FullPath);
            if (vm == null) return;
            var name = vm.Name;
            if (SelectedFolder == vm)
            {
                SelectedFolder = null;
                UpdateFolderWatcher(null);
            }
            Folders.Remove(vm);
            _allFolderItems.Remove(vm);
            ShowStatus($"Folder \"{name}\" was deleted externally and removed from the list.");
            SaveAllFolderOrder();
            OnPropertyChanged(nameof(FolderCountLabel));
        });
    }

    private void OnSubfolderCreated(object sender, FileSystemEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(FswDebounceMs);
            if (!Directory.Exists(e.FullPath)) return;
            if (_allFolderItems.Any(f => f.FullPath == e.FullPath)) return;
            var folder = new PhotoFolder { Name = e.Name ?? Path.GetFileName(e.FullPath), FullPath = e.FullPath };
            var vm = new PhotoFolderViewModel(folder);
            int insertAt = Folders.Count;
            Folders.Add(vm);
            _allFolderItems.Insert(insertAt, vm);
            SaveAllFolderOrder();
            OnPropertyChanged(nameof(FolderCountLabel));
        });
    }

    private void OnFolderFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!PhotoLibraryService.IsMediaFile(e.FullPath)) return;
        var watchedPath = (sender as FileSystemWatcher)?.Path;
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(FswDebounceMs);
            if (!File.Exists(e.FullPath)) return;
            var folder = SelectedFolder;
            if (folder == null || folder.FullPath != watchedPath) return;
            if (folder.AllPhotoItems.Any(p => p.FullPath == e.FullPath)) return;
            var item = new PhotoItem
            {
                FileName     = e.Name ?? Path.GetFileName(e.FullPath),
                FullPath     = e.FullPath,
                CreationDate = File.GetCreationTime(e.FullPath),
                IsVideo      = PhotoLibraryService.IsVideoFile(e.FullPath)
            };
            var vm = new PhotoItemViewModel(item);
            folder.AllPhotoItems.Add(vm);
            folder.Photos.Add(vm);
            OnPropertyChanged(nameof(PhotoCountLabel));
            OnPropertyChanged(nameof(FolderCountLabel));
        });
    }

    private void OnFolderFileDeleted(object sender, FileSystemEventArgs e)
    {
        var watchedPath = (sender as FileSystemWatcher)?.Path;
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var folder = SelectedFolder;
            if (folder == null || folder.FullPath != watchedPath) return;
            var vm = folder.AllPhotoItems.FirstOrDefault(p => p.FullPath == e.FullPath);
            if (vm == null) return;
            if (SelectedPhoto == vm) SelectedPhoto = null;
            folder.AllPhotoItems.Remove(vm);
            folder.Photos.Remove(vm);
            OnPropertyChanged(nameof(PhotoCountLabel));
            OnPropertyChanged(nameof(FolderCountLabel));
        });
    }

    private void OnFolderFileRenamed(object sender, RenamedEventArgs e)
    {
        var watchedPath = (sender as FileSystemWatcher)?.Path;
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var folder = SelectedFolder;
            if (folder == null || folder.FullPath != watchedPath) return;
            var vm = folder.AllPhotoItems.FirstOrDefault(p => p.FullPath == e.OldFullPath);
            if (vm == null) return;
            vm.UpdatePath(e.Name ?? Path.GetFileName(e.FullPath), e.FullPath);
        });
    }

    // Reconciles in-memory photo list with disk when a folder is selected,
    // picking up files added or removed externally while another folder was open.
    private async Task SyncFolderContentsAsync(PhotoFolderViewModel folder)
    {
        var folderPath = folder.FullPath;
        if (!Directory.Exists(folderPath)) return;

        var diskFiles = await Task.Run(() =>
            Directory.GetFiles(folderPath)
                .Where(PhotoLibraryService.IsMediaFile)
                .ToDictionary(f => f, StringComparer.OrdinalIgnoreCase));

        if (SelectedFolder != folder) return; // user navigated away during scan

        bool changed = false;

        foreach (var path in diskFiles.Keys)
        {
            if (folder.AllPhotoItems.Any(p => string.Equals(p.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                continue;
            var vm = new PhotoItemViewModel(new PhotoItem
            {
                FileName     = Path.GetFileName(path),
                FullPath     = path,
                CreationDate = File.GetCreationTime(path),
                IsVideo      = PhotoLibraryService.IsVideoFile(path)
            });
            folder.AllPhotoItems.Add(vm);
            folder.Photos.Add(vm);
            vm.EnsureThumbnailLoaded();
            changed = true;
        }

        var stale = folder.AllPhotoItems
            .Where(p => !diskFiles.ContainsKey(p.FullPath))
            .ToList();
        foreach (var vm in stale)
        {
            if (SelectedPhoto == vm) SelectedPhoto = null;
            folder.AllPhotoItems.Remove(vm);
            folder.Photos.Remove(vm);
            changed = true;
        }

        if (changed)
        {
            OnPropertyChanged(nameof(PhotoCountLabel));
            OnPropertyChanged(nameof(FolderCountLabel));
        }
    }

    partial void OnSelectedFolderChanged(PhotoFolderViewModel? value)
    {
        SelectedPhoto = null;
        if (value != null)
            _ = SyncFolderContentsAsync(value);
        value?.LoadPhotoThumbnails();
        UpdateFolderWatcher(value?.FullPath);
    }

    public async Task LoadAsync(string parentPath, string? initialFolderName = null, string? initialPhotoName = null)
    {
        ParentFolderPath = parentPath;
        var allFolders = await _library.LoadLibraryAsync(parentPath);

        Folders.Clear();
        _allFolderItems.Clear();

        // LoadLibraryAsync returns active folders first, removed at end.
        foreach (var f in allFolders)
        {
            var vm = new PhotoFolderViewModel(f);
            _allFolderItems.Add(vm);
            if (!f.IsRemoved)
                Folders.Add(vm);
        }

        SelectedFolder = (!string.IsNullOrEmpty(initialFolderName)
            ? Folders.FirstOrDefault(f => f.Name.Equals(initialFolderName, StringComparison.OrdinalIgnoreCase))
            : null) ?? Folders.FirstOrDefault();

        if (!string.IsNullOrEmpty(initialPhotoName) && SelectedFolder != null)
            SelectedPhoto = SelectedFolder.Photos.FirstOrDefault(p =>
                p.FileName.Equals(initialPhotoName, StringComparison.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(FolderCountLabel));

        _undoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));

        StartParentWatcher(ParentFolderPath);
    }

    // ── Folder operations ──────────────────────────────────────────────────────

    public void ReorderFolders(List<PhotoFolderViewModel> items, int slot)
    {
        var present = items.Where(f => Folders.Contains(f)).ToList();
        if (present.Count == 0) return;

        PushFolderUndo();

        // Adjust the insertion slot for items that will be removed ahead of it.
        int adjustedSlot = slot - present.Count(f => Folders.IndexOf(f) < slot);
        adjustedSlot = Math.Clamp(adjustedSlot, 0, Folders.Count - present.Count);

        // Remove from highest index first so lower indices stay valid.
        foreach (var f in present.OrderByDescending(f => Folders.IndexOf(f)))
        {
            _allFolderItems.Remove(f);
            Folders.Remove(f);
        }

        for (int i = 0; i < present.Count; i++)
        {
            Folders.Insert(adjustedSlot + i, present[i]);
            _allFolderItems.Insert(adjustedSlot + i, present[i]);
        }

        SaveAllFolderOrder();
    }

    public void RemoveFolders(IList<PhotoFolderViewModel> folders)
    {
        var targets = folders.Where(f => !f.IsRemoved).ToList();
        if (targets.Count == 0) return;
        PushFolderUndo();
        foreach (var f in targets)
        {
            f.IsRemoved = true;
            Folders.Remove(f);
            _allFolderItems.Move(_allFolderItems.IndexOf(f), _allFolderItems.Count - 1);
        }
        if (SelectedFolder != null && SelectedFolder.IsRemoved)
            SelectedFolder = Folders.FirstOrDefault();
        SaveAllFolderOrder();
        OnPropertyChanged(nameof(FolderCountLabel));
    }

    public void RestoreFolders(IList<PhotoFolderViewModel> folders)
    {
        var targets = folders.Where(f => f.IsRemoved).ToList();
        if (targets.Count == 0) return;
        PushFolderUndo();
        foreach (var f in targets)
        {
            f.IsRemoved = false;
            Folders.Add(f);
            _allFolderItems.Move(_allFolderItems.IndexOf(f), Folders.Count - 1);
        }
        SaveAllFolderOrder();
        OnPropertyChanged(nameof(FolderCountLabel));
    }

    public void RemoveFolder(PhotoFolderViewModel folder) => RemoveFolders(new[] { folder });
    public void RestoreFolder(PhotoFolderViewModel folder) => RestoreFolders(new[] { folder });

    // ── Photo operations ───────────────────────────────────────────────────────

    public void ReorderPhotos(List<PhotoItemViewModel> items, int slot)
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
        var present = items.Where(p => folder.Photos.Contains(p)).ToList();
        if (present.Count == 0) return;

        PushPhotoUndo(folder);

        int adjustedSlot = slot - present.Count(p => folder.Photos.IndexOf(p) < slot);
        adjustedSlot = Math.Clamp(adjustedSlot, 0, folder.Photos.Count - present.Count);

        foreach (var p in present.OrderByDescending(p => folder.Photos.IndexOf(p)))
        {
            folder.AllPhotoItems.Remove(p);
            folder.Photos.Remove(p);
        }

        for (int i = 0; i < present.Count; i++)
        {
            folder.Photos.Insert(adjustedSlot + i, present[i]);
            folder.AllPhotoItems.Insert(adjustedSlot + i, present[i]);
        }

        SaveAllPhotoOrder(folder);
    }

    public void RemovePhotos(IList<PhotoItemViewModel> photos)
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
        var targets = photos.Where(p => !p.IsRemoved).ToList();
        if (targets.Count == 0) return;
        PushPhotoUndo(folder);
        foreach (var p in targets)
        {
            p.IsRemoved = true;
            folder.Photos.Remove(p);
            folder.AllPhotoItems.Move(folder.AllPhotoItems.IndexOf(p), folder.AllPhotoItems.Count - 1);
        }
        SaveAllPhotoOrder(folder);
        OnPropertyChanged(nameof(PhotoCountLabel));
    }

    public void RestorePhotos(IList<PhotoItemViewModel> photos)
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
        var targets = photos.Where(p => p.IsRemoved).ToList();
        if (targets.Count == 0) return;
        PushPhotoUndo(folder);
        foreach (var p in targets)
        {
            p.IsRemoved = false;
            folder.Photos.Add(p);
            folder.AllPhotoItems.Move(folder.AllPhotoItems.IndexOf(p), folder.Photos.Count - 1);
        }
        SaveAllPhotoOrder(folder);
        OnPropertyChanged(nameof(PhotoCountLabel));
    }

    public void RemovePhoto(PhotoItemViewModel photo) => RemovePhotos(new[] { photo });
    public void RestorePhoto(PhotoItemViewModel photo) => RestorePhotos(new[] { photo });

    public void SetCaption(PhotoItemViewModel photo, string caption)
    {
        if (SelectedFolder != null) PushPhotoUndo(SelectedFolder);
        photo.Caption = caption;
        if (SelectedFolder != null)
            SaveAllPhotoOrder(SelectedFolder);
    }

    public void SetCaptions(IList<PhotoItemViewModel> photos, string caption)
    {
        if (SelectedFolder == null || photos.Count == 0) return;
        PushPhotoUndo(SelectedFolder);
        foreach (var p in photos)
            p.Caption = caption;
        SaveAllPhotoOrder(SelectedFolder);
    }

    public void ToggleMirrors(IList<PhotoItemViewModel> photos)
    {
        if (SelectedFolder == null || photos.Count == 0) return;
        PushPhotoUndo(SelectedFolder);
        foreach (var p in photos)
            p.IsMirrored = !p.IsMirrored;
        SaveAllPhotoOrder(SelectedFolder);
    }

    public void SortFoldersByName()
    {
        PushFolderUndo();

        var prevSelected = SelectedFolder;

        var activeSorted  = Folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var removedSorted = _allFolderItems.Where(f => f.IsRemoved)
                                           .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();

        Folders.Clear();
        _allFolderItems.Clear();

        foreach (var f in activeSorted)  { Folders.Add(f); _allFolderItems.Add(f); }
        foreach (var f in removedSorted)   _allFolderItems.Add(f);

        SaveAllFolderOrder();
        OnPropertyChanged(nameof(FolderCountLabel));

        SelectedFolder = (prevSelected != null && Folders.Contains(prevSelected))
            ? prevSelected
            : Folders.FirstOrDefault();
    }

    public async Task SortPhotosByDateAsync()
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;

        PushPhotoUndo(folder);

        var prevSelected = SelectedPhoto;

        var activeSnapshot  = folder.Photos.ToList();
        var removedSnapshot = folder.AllPhotoItems.Where(p => p.IsRemoved).ToList();

        var (sortedActive, sortedRemoved) = await Task.Run(() =>
        {
            var a = activeSnapshot .OrderBy(p => _library.GetEffectiveDateWithExif(p.Model)).ToList();
            var r = removedSnapshot.OrderBy(p => _library.GetEffectiveDateWithExif(p.Model)).ToList();
            return (a, r);
        });

        folder.Photos.Clear();
        foreach (var p in sortedActive)  folder.Photos.Add(p);

        folder.AllPhotoItems.Clear();
        foreach (var p in sortedActive)  folder.AllPhotoItems.Add(p);
        foreach (var p in sortedRemoved) folder.AllPhotoItems.Add(p);

        SaveAllPhotoOrder(folder);
        OnPropertyChanged(nameof(PhotoCountLabel));

        SelectedPhoto = (prevSelected != null && folder.Photos.Contains(prevSelected))
            ? prevSelected
            : folder.Photos.FirstOrDefault();
    }

    // ── Persistence ────────────────────────────────────────────────────────────

    private void SaveAllFolderOrder()
    {
        try { _library.SaveFolderOrder(ParentFolderPath, _allFolderItems.Select(f => f.Model)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ShowStatus("Could not save folder order — the parent folder may have been renamed or deleted."); }
    }

    private void SaveAllPhotoOrder(PhotoFolderViewModel folder)
    {
        try { _library.SavePhotoOrder(folder.Model, folder.AllPhotoItems.Select(p => p.Model)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ShowStatus("Could not save changes — the folder may have been renamed or deleted."); }
    }
}
