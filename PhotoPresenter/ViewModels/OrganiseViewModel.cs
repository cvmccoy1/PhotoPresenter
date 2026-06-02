using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoPresenter.Services;

namespace PhotoPresenter.ViewModels;

public partial class OrganiseViewModel : ObservableObject
{
    private readonly IPhotoLibraryService _library;

    public string ParentFolderPath { get; private set; } = "";
    public ObservableCollection<PhotoFolderViewModel> Folders { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Photos))]
    private PhotoFolderViewModel? _selectedFolder;

    [ObservableProperty]
    private PhotoItemViewModel? _selectedPhoto;

    public ObservableCollection<PhotoItemViewModel>? Photos => SelectedFolder?.Photos;

    partial void OnSelectedFolderChanged(PhotoFolderViewModel? value) => SelectedPhoto = null;

    public OrganiseViewModel(IPhotoLibraryService library)
    {
        _library = library;
    }

    public async Task LoadAsync(string parentPath)
    {
        ParentFolderPath = parentPath;
        var folders = await _library.LoadLibraryAsync(parentPath);

        Folders.Clear();
        foreach (var f in folders)
            Folders.Add(new PhotoFolderViewModel(f));

        SelectedFolder = Folders.Count > 0 ? Folders[0] : null;
    }

    public void ReorderFolder(int from, int to)
    {
        Folders.Move(from, to);
        _library.SaveFolderOrder(ParentFolderPath, Folders.Select(f => f.Model));
    }

    public void ReorderPhoto(int from, int to)
    {
        if (SelectedFolder == null) return;
        SelectedFolder.Photos.Move(from, to);
        _library.SavePhotoOrder(SelectedFolder.Model, SelectedFolder.Photos.Select(p => p.Model));
    }

    public void RemoveFolder(PhotoFolderViewModel folder)
    {
        Folders.Remove(folder);
        if (SelectedFolder == folder)
            SelectedFolder = Folders.FirstOrDefault();
        _library.SaveFolderOrder(ParentFolderPath, Folders.Select(f => f.Model));
    }

    public void RemovePhoto(PhotoItemViewModel photo)
    {
        if (SelectedFolder == null) return;
        SelectedFolder.Photos.Remove(photo);
        _library.SavePhotoOrder(SelectedFolder.Model, SelectedFolder.Photos.Select(p => p.Model));
    }
}
