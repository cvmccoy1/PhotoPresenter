using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Integration;

/// <summary>
/// Verifies that OrganiseViewModel FSW event handler lambda bodies execute.
/// Without [Collection("WPF")], Application.Current is null and the ?. guard
/// swallows the Dispatcher.InvokeAsync call, leaving the lambda body uncovered.
/// These tests rely on [Collection("WPF")] to ensure Application.Current is set.
/// </summary>
[Collection("WPF")]
public class FswHandlerTests : IDisposable
{
    private readonly WpfApplicationFixture _fixture;
    private readonly TempDirectory _tmp = new();
    private OrganiseViewModel? _vm;

    public FswHandlerTests(WpfApplicationFixture fixture) => _fixture = fixture;

    public void Dispose()
    {
        _vm?.Dispose();
        _tmp.Dispose();
    }

    // Creates a VM with one subfolder ("Album") containing one photo, waits for
    // the initial SyncFolderContentsAsync to settle, then returns the VM.
    private async Task<OrganiseViewModel> BuildVmAsync()
    {
        _tmp.CreateSubDir("Album");
        _tmp.CreateFile(@"Album\photo1.jpg", TempDirectory.TinyJpegBytes);
        _vm = new OrganiseViewModel(new PhotoLibraryService());
        await _vm.LoadAsync(_tmp.Path);
        await Task.Delay(300); // let SyncFolderContentsAsync settle
        return _vm;
    }

    // Reads state on the WPF dispatcher thread — safe because FSW handlers
    // also dispatch their writes through Application.Current.Dispatcher.
    private T OnDispatcher<T>(Func<T> read) => _fixture.Invoke(read);

    // ── OnSubfolderCreated (lines 346-360) ────────────────────────────────────────

    [Fact]
    public async Task FswHandler_SubfolderCreated_NewFolderAppearsInVm()
    {
        var vm = await BuildVmAsync();
        Assert.Equal(1, OnDispatcher(() => vm.Folders.Count));

        // New subfolder → _parentWatcher.Created → OnSubfolderCreated lambda
        Directory.CreateDirectory(Path.Combine(_tmp.Path, "NewAlbum"));

        await Task.Delay(700); // FSW detection + 150 ms debounce + dispatcher overhead

        Assert.Equal(2, OnDispatcher(() => vm.Folders.Count));
        Assert.Contains(OnDispatcher(() => vm.Folders.ToList()), f => f.Name == "NewAlbum");
    }

    // ── OnSubfolderRenamed (lines 313-323) ────────────────────────────────────────

    [Fact]
    public async Task FswHandler_SubfolderRenamed_VmNameUpdated()
    {
        var vm = await BuildVmAsync();
        var oldPath = Path.Combine(_tmp.Path, "Album");
        var newPath = Path.Combine(_tmp.Path, "RenamedAlbum");

        // Rename subfolder → _parentWatcher.Renamed → OnSubfolderRenamed lambda
        Directory.Move(oldPath, newPath);

        await Task.Delay(400); // no debounce in this handler; just FSW + dispatcher

        string name = OnDispatcher(() => vm.Folders[0].Name);
        Assert.Equal("RenamedAlbum", name);
    }

    // ── OnFolderFileCreated lambda body (lines 367-386) ───────────────────────────

    [Fact]
    public async Task FswHandler_FileCreated_NewPhotoAppearsInSelectedFolder()
    {
        var vm = await BuildVmAsync();
        Assert.Equal(1, OnDispatcher(() => vm.SelectedFolder!.Photos.Count));

        // New file in selected folder → _folderWatcher.Created → OnFolderFileCreated lambda
        File.WriteAllBytes(
            Path.Combine(_tmp.Path, "Album", "photo2.jpg"),
            TempDirectory.TinyJpegBytes);

        await Task.Delay(700); // FSW detection + 150 ms debounce + dispatcher overhead

        Assert.Equal(2, OnDispatcher(() => vm.SelectedFolder!.Photos.Count));
        Assert.Contains(OnDispatcher(() => vm.SelectedFolder!.Photos.ToList()),
            p => p.FileName == "photo2.jpg");
    }

    // ── OnFolderFileRenamed — existing-VM path (lines 408-418) ───────────────────

    [Fact]
    public async Task FswHandler_FileRenamed_ExistingVmPathUpdated()
    {
        var vm = await BuildVmAsync();
        var oldPath = Path.Combine(_tmp.Path, "Album", "photo1.jpg");
        var newPath = Path.Combine(_tmp.Path, "Album", "renamed.jpg");

        // Rename a tracked media file → OnFolderFileRenamed, vm != null branch (UpdatePath)
        File.Move(oldPath, newPath);

        await Task.Delay(400); // no debounce on the UpdatePath branch

        string fileName = OnDispatcher(() => vm.SelectedFolder!.Photos[0].FileName);
        Assert.Equal("renamed.jpg", fileName);
    }

    // ── OnFolderFileRenamed — new-file path (lines 422-440) ──────────────────────

    [Fact]
    public async Task FswHandler_NonMediaFileRenamedToMedia_TreatedAsNewPhoto()
    {
        var vm = await BuildVmAsync();
        Assert.Equal(1, OnDispatcher(() => vm.SelectedFolder!.Photos.Count));

        // Place a .tmp file (no PhotoItemViewModel created for it)
        var tmpPath   = Path.Combine(_tmp.Path, "Album", "encoding.tmp");
        var mediaPath = Path.Combine(_tmp.Path, "Album", "output.jpg");
        File.WriteAllBytes(tmpPath, TempDirectory.TinyJpegBytes);

        // Rename .tmp → .jpg: OnFolderFileRenamed fires, no VM found for old path,
        // falls through to the "treat as new addition" path (lines 422-440).
        File.Move(tmpPath, mediaPath);

        await Task.Delay(700); // 150 ms debounce in the new-addition branch

        Assert.Equal(2, OnDispatcher(() => vm.SelectedFolder!.Photos.Count));
        Assert.Contains(OnDispatcher(() => vm.SelectedFolder!.Photos.ToList()),
            p => p.FileName == "output.jpg");
    }
}
