using NSubstitute;
using PhotoPresenter.Models;
using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Integration;

/// <summary>
/// Integration tests for OrganiseViewModel.MovePhotosToFolder and its undo via
/// ApplyCrossFolderMoveSnapshot. Uses real temp directories so File.Move calls succeed,
/// but a mock IPhotoLibraryService so sidecar JSON is not written to disk.
///
/// Race-condition note: setting SelectedFolder fires a background SyncFolderContentsAsync.
/// We await a 200 ms delay (same as OrganiseSyncTests) before calling MovePhotosToFolder
/// so the initial sync completes and sees a consistent state, preventing a phantom-VM race
/// where the stale disk scan tries to re-add a file that was already removed by the move.
///
/// GetUniqueDestinationName is covered indirectly through the duplicate-name test.
/// </summary>
public class CrossFolderMoveTests
{
    private static OrganiseViewModel MakeVm()
    {
        var lib = Substitute.For<IPhotoLibraryService>();
        return new OrganiseViewModel(lib);
    }

    /// <summary>
    /// Creates a temp sub-folder with the specified files; returns a PhotoFolderViewModel
    /// whose FullPath is the real directory so File.Move operations succeed.
    /// </summary>
    private static (PhotoFolderViewModel FolderVm, string Dir) MakeFolder(
        TempDirectory tmp, string name, params string[] fileNames)
    {
        var dir    = Path.Combine(tmp.Path, name);
        var folder = new PhotoFolder { Name = name, FullPath = dir };
        foreach (var fn in fileNames)
        {
            var path = tmp.CreateFile(Path.Combine(name, fn));
            folder.Photos.Add(new PhotoItem { FileName = fn, FullPath = path });
        }
        return (new PhotoFolderViewModel(folder), dir);
    }

    // ── Basic move ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MovePhotosToFolder_MovesPhotoFromSourceToTarget()
    {
        using var tmp = new TempDirectory();
        var (src, srcDir) = MakeFolder(tmp, "Source", "photo1.jpg");
        var (dst, dstDir) = MakeFolder(tmp, "Target", "other.jpg");
        var vm = MakeVm();
        vm.SelectedFolder = src;
        await Task.Delay(200); // let fire-and-forget SyncFolderContentsAsync complete
        var photo = src.Photos[0];

        vm.MovePhotosToFolder(new List<PhotoItemViewModel> { photo }, dst);

        Assert.Empty(src.Photos);
        Assert.Equal(2, dst.Photos.Count);
        Assert.True(File.Exists(Path.Combine(dstDir, "photo1.jpg")));
        Assert.False(File.Exists(Path.Combine(srcDir, "photo1.jpg")));
    }

    // ── Guard clauses ─────────────────────────────────────────────────────────────

    [Fact]
    public void MovePhotosToFolder_SameSourceAndTarget_IsNoOp()
    {
        using var tmp = new TempDirectory();
        var (src, srcDir) = MakeFolder(tmp, "Source", "photo1.jpg");
        var vm = MakeVm();
        vm.SelectedFolder = src;

        vm.MovePhotosToFolder(new List<PhotoItemViewModel> { src.Photos[0] }, src);

        Assert.Single(src.Photos);
        Assert.True(File.Exists(Path.Combine(srcDir, "photo1.jpg")));
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void MovePhotosToFolder_EmptyPhotoList_IsNoOp()
    {
        using var tmp = new TempDirectory();
        var (src, srcDir) = MakeFolder(tmp, "Source", "photo1.jpg");
        var (dst, _)      = MakeFolder(tmp, "Target", "other.jpg");
        var vm = MakeVm();
        vm.SelectedFolder = src;

        vm.MovePhotosToFolder(new List<PhotoItemViewModel>(), dst);

        Assert.Single(src.Photos);
        Assert.True(File.Exists(Path.Combine(srcDir, "photo1.jpg")));
        Assert.False(vm.CanUndo);
    }

    // ── GetUniqueDestinationName (via duplicate-name scenario) ───────────────────

    [Fact]
    public async Task MovePhotosToFolder_DuplicateFilename_DeconflictsWithParenthesisSuffix()
    {
        using var tmp = new TempDirectory();
        var (src, srcDir) = MakeFolder(tmp, "Source", "photo.jpg");
        var (dst, dstDir) = MakeFolder(tmp, "Target", "photo.jpg"); // same name already exists
        var vm = MakeVm();
        vm.SelectedFolder = src;
        await Task.Delay(200);
        var photo = src.Photos[0];

        vm.MovePhotosToFolder(new List<PhotoItemViewModel> { photo }, dst);

        Assert.False(File.Exists(Path.Combine(srcDir, "photo.jpg")));
        Assert.True(File.Exists(Path.Combine(dstDir, "photo (1).jpg")));
        Assert.Equal("photo (1).jpg", photo.FileName); // VM path updated to match
    }

    // ── Undo state ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MovePhotosToFolder_AfterSuccessfulMove_PushesUndoEntry()
    {
        using var tmp = new TempDirectory();
        var (src, _) = MakeFolder(tmp, "Source", "photo1.jpg");
        var (dst, _) = MakeFolder(tmp, "Target", "other.jpg");
        var vm = MakeVm();
        vm.SelectedFolder = src;
        await Task.Delay(200);

        Assert.False(vm.CanUndo);
        vm.MovePhotosToFolder(new List<PhotoItemViewModel> { src.Photos[0] }, dst);
        Assert.True(vm.CanUndo);
    }

    // ── ApplyCrossFolderMoveSnapshot (via Undo) ───────────────────────────────────

    [Fact]
    public async Task Undo_AfterCrossFolderMove_RestoresBothFolders()
    {
        using var tmp = new TempDirectory();
        var (src, srcDir) = MakeFolder(tmp, "Source", "photo1.jpg");
        var (dst, dstDir) = MakeFolder(tmp, "Target", "other.jpg");
        var vm = MakeVm();
        vm.SelectedFolder = src;
        await Task.Delay(200);
        var photo = src.Photos[0];

        vm.MovePhotosToFolder(new List<PhotoItemViewModel> { photo }, dst);
        Assert.Empty(src.Photos);
        Assert.Equal(2, dst.Photos.Count);

        vm.Undo();

        // Source folder should have its photo back.
        Assert.Single(src.Photos);
        // Target folder should be back to its original single photo.
        Assert.Single(dst.Photos);
        // File should be back on disk in the source directory.
        Assert.True(File.Exists(Path.Combine(srcDir, "photo1.jpg")));
        Assert.False(File.Exists(Path.Combine(dstDir, "photo1.jpg")));
    }
}
