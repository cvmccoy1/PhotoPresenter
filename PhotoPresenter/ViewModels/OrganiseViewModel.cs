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
    private bool _showAllFolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPhotoItems))]
    [NotifyPropertyChangedFor(nameof(PhotoCountLabel))]
    private bool _showAllPhotos;

    // Switches between active-only Folders and all-items list.
    public ObservableCollection<PhotoFolderViewModel> CurrentFolderItems =>
        ShowAllFolders ? _allFolderItems : Folders;

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
            int active  = SelectedFolder.Photos.Count;
            int removed = SelectedFolder.AllPhotoItems.Count - active;
            if (ShowAllPhotos && removed > 0)
                return $"{active + removed} photos  ({removed} removed)";
            return $"{active} photo{(active == 1 ? "" : "s")}";
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
        // Move to end of _allFolderItems (the removed section).
        _allFolderItems.Move(_allFolderItems.IndexOf(folder), _allFolderItems.Count - 1);
        SaveAllFolderOrder();
    }

    public void RestoreFolder(PhotoFolderViewModel folder)
    {
        folder.IsRemoved = false;
        Folders.Add(folder);
        // Move in _allFolderItems to the last active slot.
        _allFolderItems.Move(_allFolderItems.IndexOf(folder), Folders.Count - 1);
        SaveAllFolderOrder();
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

    // ── Persistence ────────────────────────────────────────────────────────────

    private void SaveAllFolderOrder() =>
        _library.SaveFolderOrder(ParentFolderPath, _allFolderItems.Select(f => f.Model));

    private void SaveAllPhotoOrder(PhotoFolderViewModel folder) =>
        _library.SavePhotoOrder(folder.Model, folder.AllPhotoItems.Select(p => p.Model));
}
