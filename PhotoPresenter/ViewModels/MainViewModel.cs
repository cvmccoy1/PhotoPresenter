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

    [ObservableProperty]
    private bool _favoritesOnly;

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
            _ = OrganiseVM.LoadAsync(settings.LastParentFolder, settings.LastSelectedFolder, settings.LastSelectedPhoto);
        }
        OrganiseVM.ShowAllFolders = settings.ShowAllFolders;
        OrganiseVM.ShowAllPhotos  = settings.ShowAllPhotos;
        PresentVM.Volume          = settings.Volume;
        PresentVM.IsFadeEnabled   = settings.FadeTransitionEnabled;
        PresentVM.AutoplayIntervalSeconds = settings.AutoplayIntervalSeconds;
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
        var allFolders = OrganiseVM.Folders.ToList();
        if (allFolders.Count == 0) return;

        if (FavoritesOnly)
        {
            var compacted = allFolders
                .Select(f => (folder: f, photos: f.Photos.Where(p => p.IsFavorite).ToList()))
                .Where(x => x.photos.Count > 0)
                .ToList();

            if (compacted.Count == 0)
            {
                OrganiseVM.ShowStatus("No favorites to present.");
                return;
            }

            int startFolder = 0, startPhoto = 0;
            if (OrganiseVM.SelectedFolder != null)
            {
                int fi = compacted.FindIndex(x => x.folder == OrganiseVM.SelectedFolder);
                if (fi >= 0)
                {
                    startFolder = fi;
                    if (OrganiseVM.SelectedPhoto?.IsFavorite == true)
                        startPhoto = Math.Max(0, compacted[fi].photos.IndexOf(OrganiseVM.SelectedPhoto));
                }
            }

            PresentVM.SetFolders(
                compacted.Select(x => x.folder).ToList(),
                startFolder, startPhoto,
                compacted.Select(x => (IReadOnlyList<PhotoItemViewModel>)x.photos).ToList());
        }
        else
        {
            int startFolder = 0, startPhoto = 0;
            if (OrganiseVM.SelectedFolder != null)
            {
                startFolder = Math.Max(0, allFolders.IndexOf(OrganiseVM.SelectedFolder));
                if (OrganiseVM.SelectedPhoto != null)
                    startPhoto = Math.Max(0, OrganiseVM.SelectedFolder.Photos.IndexOf(OrganiseVM.SelectedPhoto));
            }
            PresentVM.SetFolders(allFolders, startFolder, startPhoto);
        }

        CurrentMode = AppMode.Present;
    }

    public void SwitchToOrganise()
    {
        var lastFolder = PresentVM.CurrentFolder;
        var lastPhoto  = PresentVM.CurrentPhotoItem;

        if (lastFolder != null && lastPhoto != null)
        {
            OrganiseVM.SelectedFolder = lastFolder;
            OrganiseVM.SelectedPhoto  = lastPhoto;
            OrganiseVM.ScrollPhotoIntoViewRequested = true;
        }

        CurrentMode = AppMode.Organise;
    }
}
