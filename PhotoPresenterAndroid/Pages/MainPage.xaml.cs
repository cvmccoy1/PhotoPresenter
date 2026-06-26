using CommunityToolkit.Maui.Storage;
using PhotoPresenterAndroid.Services;

namespace PhotoPresenterAndroid.Pages;

public partial class MainPage : ContentPage
{
    private string _folderPath = "";

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CheckPermissions();

        // Restore last folder from preferences.
        var saved = Preferences.Default.Get("LastFolder", "");
        if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            ApplyFolder(saved, notify: false);
    }

    private void CheckPermissions()
    {
        try
        {
#if ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                bool hasAll = Android.OS.Environment.IsExternalStorageManager;
                PermissionBanner.IsVisible = !hasAll;
                GrantPermissionButton.IsVisible = !hasAll;
            }
#endif
        }
        catch { /* permission check failure is non-fatal */ }
    }

    private void ApplyFolder(string path, bool notify = true)
    {
        _folderPath = path;
        FolderPathLabel.Text = path;

        bool hasManifest = ManifestService.HasManifest(path);
        OpenPresentationButton.IsEnabled = hasManifest;

        if (notify)
        {
            StatusLabel.Text = hasManifest
                ? $"Found presentation with files ready to view."
                : "No _presentation.json found in this folder.";
        }
        else
        {
            StatusLabel.Text = "";
        }

        Preferences.Default.Set("LastFolder", path);
    }

    private async void SelectFolder_Clicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (!result.IsSuccessful) return;

            string path = result.Folder.Path;

            // On Android, FolderPicker may return a content URI; try to resolve it.
#if ANDROID
            path = ResolveAndroidPath(path) ?? path;
#endif

            if (!Directory.Exists(path))
            {
                StatusLabel.Text = "Could not access the selected folder. Try entering the path manually.";
                return;
            }

            ApplyFolder(path);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OpenPresentation_Clicked(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Loading…";
            Android.Util.Log.Debug("PP_DIAG", $"OpenPresentation: folder={_folderPath}");
            var items = await ManifestService.LoadAsync(_folderPath);
            Android.Util.Log.Debug("PP_DIAG", $"OpenPresentation: items={items.Count}");
            if (items.Count == 0)
            {
                StatusLabel.Text = "No presentable items found. Check that the folder contains exported files.";
                return;
            }
            StatusLabel.Text = "";
            Android.Util.Log.Debug("PP_DIAG", "OpenPresentation: calling GoToAsync");
            await Shell.Current.GoToAsync(nameof(PresentPage),
                new Dictionary<string, object> { ["Items"] = items });
            Android.Util.Log.Debug("PP_DIAG", "OpenPresentation: GoToAsync returned");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("PP_DIAG", $"OpenPresentation EXCEPTION: {ex}");
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void GrantPermission_Clicked(object sender, EventArgs e)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            bool proceed = await DisplayAlert("Permission Required",
                "Photo Presenter needs 'All files access' to read photos and presentation files. " +
                "On the next screen, enable 'Allow management of all files' for this app.",
                "Open Settings", "Cancel");

            if (proceed)
            {
                var intent = new Android.Content.Intent(
                    Android.Provider.Settings.ActionManageAppAllFilesAccessPermission,
                    Android.Net.Uri.Parse($"package:{AppInfo.PackageName}"));
                Platform.CurrentActivity?.StartActivity(intent);
            }
        }
#endif
    }

#if ANDROID
    private static string? ResolveAndroidPath(string path)
    {
        if (!path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            return path; // Already a real path.

        try
        {
            var uri = Android.Net.Uri.Parse(path);
            var docId = Android.Provider.DocumentsContract.GetTreeDocumentId(uri);
            if (docId?.StartsWith("primary:") == true)
            {
                var extRoot = Android.OS.Environment.ExternalStorageDirectory!.AbsolutePath;
                return Path.Combine(extRoot, docId[8..]);
            }
        }
        catch { }
        return null;
    }
#endif
}
