using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PhotoPresenter.Services;

namespace PhotoPresenter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPhotoLibraryService _library;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentView))]
    private AppMode _currentMode = AppMode.Organise;

    [ObservableProperty]
    private string _parentFolderPath = "";

    public OrganiseViewModel OrganiseVM { get; }
    public PresentViewModel PresentVM { get; }

    public object CurrentView => CurrentMode == AppMode.Present ? (object)PresentVM : OrganiseVM;

    public MainViewModel()
    {
        _library = new PhotoLibraryService();
        OrganiseVM = new OrganiseViewModel(_library);
        PresentVM = new PresentViewModel();

        var settings = UserSettings.Load();
        if (!string.IsNullOrEmpty(settings.LastParentFolder))
        {
            ParentFolderPath = settings.LastParentFolder;
            _ = OrganiseVM.LoadAsync(settings.LastParentFolder, settings.LastSelectedFolder);
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the photo library folder"
        };
        if (dialog.ShowDialog() != true) return;

        ParentFolderPath = dialog.FolderName;
        new UserSettings { LastParentFolder = ParentFolderPath }.Save();
        _ = OrganiseVM.LoadAsync(ParentFolderPath);
    }

    [RelayCommand]
    private void SwitchToPresent()
    {
        if (OrganiseVM.Folders.Count == 0) return;

        var folders = OrganiseVM.Folders.ToList();
        int startFolder = 0;
        int startPhoto = 0;

        if (OrganiseVM.SelectedFolder != null)
        {
            startFolder = Math.Max(0, folders.IndexOf(OrganiseVM.SelectedFolder));
            if (OrganiseVM.SelectedPhoto != null)
                startPhoto = Math.Max(0, OrganiseVM.SelectedFolder.Photos.IndexOf(OrganiseVM.SelectedPhoto));
        }

        PresentVM.SetFolders(folders, startFolder, startPhoto);
        CurrentMode = AppMode.Present;
    }

    public void SwitchToOrganise() => CurrentMode = AppMode.Organise;
}
