using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Integration;

/// <summary>
/// Integration tests for OrganiseViewModel.SyncFolderContentsAsync.
/// Uses real temp directories so the method's Directory.GetFiles calls work correctly.
/// A short delay after LoadAsync lets the fire-and-forget initial sync complete before
/// the test manipulates disk state to avoid race conditions.
/// </summary>
public class OrganiseSyncTests
{
    // ── New file appears on disk ──────────────────────────────────────────────────

    [Fact]
    public async Task SyncFolderContentsAsync_FileAddedToDisk_AppearsInVm()
    {
        using var tmp = new TempDirectory();
        tmp.CreateFile(@"Photos\photo1.jpg");

        var vm = new OrganiseViewModel(new PhotoLibraryService());
        await vm.LoadAsync(tmp.Path);
        await Task.Delay(200); // let fire-and-forget initial sync complete

        Assert.Single(vm.SelectedFolder!.Photos);

        tmp.CreateFile(@"Photos\photo2.jpg");
        await vm.SyncFolderContentsAsync(vm.SelectedFolder);

        Assert.Equal(2, vm.SelectedFolder.Photos.Count);
        Assert.Contains(vm.SelectedFolder.Photos, p => p.FileName == "photo2.jpg");
    }

    // ── Stale file removed from disk ──────────────────────────────────────────────

    [Fact]
    public async Task SyncFolderContentsAsync_FileDeletedFromDisk_RemovedFromVm()
    {
        using var tmp = new TempDirectory();
        var file1 = tmp.CreateFile(@"Photos\photo1.jpg");
        tmp.CreateFile(@"Photos\photo2.jpg");

        var vm = new OrganiseViewModel(new PhotoLibraryService());
        await vm.LoadAsync(tmp.Path);
        await Task.Delay(200);

        Assert.Equal(2, vm.SelectedFolder!.Photos.Count);

        File.Delete(file1);
        await vm.SyncFolderContentsAsync(vm.SelectedFolder);

        Assert.Single(vm.SelectedFolder.Photos);
        Assert.Equal("photo2.jpg", vm.SelectedFolder.Photos[0].FileName);
    }

    // ── No changes — VM left unmodified ──────────────────────────────────────────

    [Fact]
    public async Task SyncFolderContentsAsync_NoDiskChanges_PhotoCountUnchanged()
    {
        using var tmp = new TempDirectory();
        tmp.CreateFile(@"Photos\photo1.jpg");

        var vm = new OrganiseViewModel(new PhotoLibraryService());
        await vm.LoadAsync(tmp.Path);
        await Task.Delay(200);

        int before = vm.SelectedFolder!.Photos.Count;
        await vm.SyncFolderContentsAsync(vm.SelectedFolder);

        Assert.Equal(before, vm.SelectedFolder.Photos.Count);
    }

    // ── Non-media files ignored ───────────────────────────────────────────────────

    [Fact]
    public async Task SyncFolderContentsAsync_NonMediaFileOnDisk_NotAddedToVm()
    {
        using var tmp = new TempDirectory();
        var photosDir = tmp.CreateSubDir("Photos");
        tmp.CreateFile(@"Photos\photo1.jpg");
        File.WriteAllText(Path.Combine(photosDir, "notes.txt"), "text");

        var vm = new OrganiseViewModel(new PhotoLibraryService());
        await vm.LoadAsync(tmp.Path);
        await Task.Delay(200);

        await vm.SyncFolderContentsAsync(vm.SelectedFolder!);

        Assert.Single(vm.SelectedFolder!.Photos);
    }

    // ── Folder path does not exist — early return ─────────────────────────────────

    [Fact]
    public async Task SyncFolderContentsAsync_FolderNotOnDisk_ReturnsImmediately()
    {
        using var tmp = new TempDirectory();
        var vm = new OrganiseViewModel(new PhotoLibraryService());

        var ghostFolder = new PhotoPresenter.Models.PhotoFolder
        {
            Name     = "Ghost",
            FullPath = Path.Combine(tmp.Path, "DoesNotExist")
        };
        var folderVm = new PhotoPresenter.ViewModels.PhotoFolderViewModel(ghostFolder);

        // Must not throw — returns early because directory doesn't exist.
        await vm.SyncFolderContentsAsync(folderVm);

        Assert.Empty(folderVm.Photos);
    }

    // ── SelectedPhoto cleared when stale file is the selected photo ───────────────

    [Fact]
    public async Task SyncFolderContentsAsync_SelectedPhotoDeleted_SelectedPhotoCleared()
    {
        using var tmp = new TempDirectory();
        var file1 = tmp.CreateFile(@"Photos\photo1.jpg");
        tmp.CreateFile(@"Photos\photo2.jpg");

        var vm = new OrganiseViewModel(new PhotoLibraryService());
        await vm.LoadAsync(tmp.Path);
        await Task.Delay(200);

        vm.SelectedPhoto = vm.SelectedFolder!.Photos.First(p => p.FileName == "photo1.jpg");
        File.Delete(file1);

        await vm.SyncFolderContentsAsync(vm.SelectedFolder);

        Assert.Null(vm.SelectedPhoto);
    }
}
