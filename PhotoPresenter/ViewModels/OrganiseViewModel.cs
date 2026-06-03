using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Services;

namespace PhotoPresenter.ViewModels;

public partial class OrganiseViewModel : ObservableObject
{
    private readonly IPhotoLibraryService _library;

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
            if (ShowAllFolders && removed > 0)
                return $"{active + removed} folders  ({removed} removed)";
            return $"{active} folder{(active == 1 ? "" : "s")}";
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
            int photoCount = SelectedFolder.Photos.Count(p => !p.IsVideo);
            int videoCount = SelectedFolder.Photos.Count(p =>  p.IsVideo);
            int total      = photoCount + videoCount;
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

    public OrganiseViewModel(IPhotoLibraryService library)
    {
        _library = library;
    }

    partial void OnSelectedFolderChanged(PhotoFolderViewModel? value) => SelectedPhoto = null;

    public async Task LoadAsync(string parentPath)
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

        SelectedFolder = Folders.Count > 0 ? Folders[0] : null;
        OnPropertyChanged(nameof(FolderCountLabel));
    }

    // ── Folder operations ──────────────────────────────────────────────────────

    public void ReorderFolder(int from, int to)
    {
        var item = Folders[from];
        Folders.Move(from, to);
        // Active items occupy the first Folders.Count slots in _allFolderItems.
        _allFolderItems.Move(_allFolderItems.IndexOf(item), to);
        SaveAllFolderOrder();
    }

    public void RemoveFolder(PhotoFolderViewModel folder)
    {
        folder.IsRemoved = true;
        Folders.Remove(folder);
        if (SelectedFolder == folder)
            SelectedFolder = Folders.FirstOrDefault();
        _allFolderItems.Move(_allFolderItems.IndexOf(folder), _allFolderItems.Count - 1);
        SaveAllFolderOrder();
        OnPropertyChanged(nameof(FolderCountLabel));
    }

    public void RestoreFolder(PhotoFolderViewModel folder)
    {
        folder.IsRemoved = false;
        Folders.Add(folder);
        _allFolderItems.Move(_allFolderItems.IndexOf(folder), Folders.Count - 1);
        SaveAllFolderOrder();
        OnPropertyChanged(nameof(FolderCountLabel));
    }

    // ── Photo operations ───────────────────────────────────────────────────────

    public void ReorderPhoto(int from, int to)
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
        var item = folder.Photos[from];
        folder.Photos.Move(from, to);
        // Active items occupy the first Photos.Count slots in AllPhotoItems.
        folder.AllPhotoItems.Move(folder.AllPhotoItems.IndexOf(item), to);
        SaveAllPhotoOrder(folder);
    }

    public void RemovePhoto(PhotoItemViewModel photo)
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
        photo.IsRemoved = true;
        folder.Photos.Remove(photo);
        folder.AllPhotoItems.Move(folder.AllPhotoItems.IndexOf(photo), folder.AllPhotoItems.Count - 1);
        SaveAllPhotoOrder(folder);
        OnPropertyChanged(nameof(PhotoCountLabel));
    }

    public void RestorePhoto(PhotoItemViewModel photo)
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
        photo.IsRemoved = false;
        folder.Photos.Add(photo);
        folder.AllPhotoItems.Move(folder.AllPhotoItems.IndexOf(photo), folder.Photos.Count - 1);
        SaveAllPhotoOrder(folder);
        OnPropertyChanged(nameof(PhotoCountLabel));
    }

    public async Task SortPhotosByDateAsync()
    {
        if (SelectedFolder == null) return;
        var folder = SelectedFolder;
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

    private void SaveAllFolderOrder() =>
        _library.SaveFolderOrder(ParentFolderPath, _allFolderItems.Select(f => f.Model));

    private void SaveAllPhotoOrder(PhotoFolderViewModel folder) =>
        _library.SavePhotoOrder(folder.Model, folder.AllPhotoItems.Select(p => p.Model));
}
