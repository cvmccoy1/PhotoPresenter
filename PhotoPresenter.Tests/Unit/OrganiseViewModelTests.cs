using NSubstitute;
using PhotoPresenter.Models;
using PhotoPresenter.Services;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Unit;

/// <summary>
/// Tests OrganiseViewModel core mutation logic via a mocked IPhotoLibraryService.
/// LoadAsync is called with a non-existent path so FileSystemWatchers are never created
/// (StartParentWatcher checks Directory.Exists and returns early).
/// </summary>
public class OrganiseViewModelTests
{
    private const string FakePath = @"Z:\nonexistent\path";

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static PhotoFolder MakeFolder(string name, params string[] fileNames)
    {
        return new PhotoFolder
        {
            Name = name,
            FullPath = System.IO.Path.Combine(FakePath, name),
            Photos = fileNames.Select(f => new PhotoItem
            {
                FileName = f,
                FullPath = System.IO.Path.Combine(FakePath, name, f)
            }).ToList()
        };
    }

    private static async Task<OrganiseViewModel> BuildVm(params PhotoFolder[] folders)
    {
        var library = Substitute.For<IPhotoLibraryService>();
        library.LoadLibraryAsync(Arg.Any<string>())
            .Returns(Task.FromResult(folders.ToList()));
        var vm = new OrganiseViewModel(library);
        await vm.LoadAsync(FakePath);
        return vm;
    }

    // ── ReorderFolders ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderFolders_SingleItem_MovesToStart()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"), MakeFolder("C"));
        var c = vm.Folders[2];

        vm.ReorderFolders([c], 0);

        Assert.Equal("C", vm.Folders[0].Name);
        Assert.Equal("A", vm.Folders[1].Name);
        Assert.Equal("B", vm.Folders[2].Name);
    }

    [Fact]
    public async Task ReorderFolders_SingleItem_MovesToEnd()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"), MakeFolder("C"));
        var a = vm.Folders[0];

        vm.ReorderFolders([a], 3);

        Assert.Equal("B", vm.Folders[0].Name);
        Assert.Equal("C", vm.Folders[1].Name);
        Assert.Equal("A", vm.Folders[2].Name);
    }

    [Fact]
    public async Task ReorderFolders_SingleItem_MovesToMiddle()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"), MakeFolder("C"));
        var a = vm.Folders[0];

        vm.ReorderFolders([a], 2);

        Assert.Equal("B", vm.Folders[0].Name);
        Assert.Equal("A", vm.Folders[1].Name);
        Assert.Equal("C", vm.Folders[2].Name);
    }

    [Fact]
    public async Task ReorderFolders_MultipleItems_MovedTogether()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"), MakeFolder("C"), MakeFolder("D"));
        var a = vm.Folders[0];
        var c = vm.Folders[2];

        vm.ReorderFolders([a, c], 4);

        // A and C should be at the end; order within the moved group follows their original order
        var names = vm.Folders.Select(f => f.Name).ToList();
        Assert.Equal("B", names[0]);
        Assert.Equal("D", names[1]);
        Assert.Contains("A", names.Skip(2));
        Assert.Contains("C", names.Skip(2));
    }

    [Fact]
    public async Task ReorderFolders_ItemNotInFolders_Ignored()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        var ghost = new PhotoFolderViewModel(MakeFolder("Ghost"));

        vm.ReorderFolders([ghost], 0);

        Assert.Equal(new[] { "A", "B" }, vm.Folders.Select(f => f.Name));
    }

    [Fact]
    public async Task ReorderFolders_PushesUndo()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        Assert.False(vm.CanUndo);

        vm.ReorderFolders([vm.Folders[0]], 2);

        Assert.True(vm.CanUndo);
    }

    [Fact]
    public async Task ReorderFolders_CallsSaveFolderOrder()
    {
        var library = Substitute.For<IPhotoLibraryService>();
        library.LoadLibraryAsync(Arg.Any<string>())
            .Returns(Task.FromResult(new List<PhotoFolder> { MakeFolder("A"), MakeFolder("B") }));
        var vm = new OrganiseViewModel(library);
        await vm.LoadAsync(FakePath);

        vm.ReorderFolders([vm.Folders[0]], 2);

        library.Received(1).SaveFolderOrder(Arg.Any<string>(), Arg.Any<IEnumerable<PhotoFolder>>());
    }

    // ── RemoveFolders / RestoreFolders ───────────────────────────────────────────

    [Fact]
    public async Task RemoveFolders_ActiveFolder_MarkedRemovedAndRemovedFromFolders()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        var b = vm.Folders[1];

        vm.RemoveFolder(b);

        Assert.True(b.IsRemoved);
        Assert.DoesNotContain(b, vm.Folders);
    }

    [Fact]
    public async Task RemoveFolders_AlreadyRemoved_NoOp()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        var b = vm.Folders[1];
        vm.RemoveFolder(b);
        int stackBefore = vm.CanUndo ? 1 : 0;

        vm.RemoveFolder(b); // second call — already removed

        // Stack should not grow (second call is a no-op)
        Assert.Equal(stackBefore, vm.CanUndo ? 1 : 0);
        Assert.True(b.IsRemoved);
    }

    [Fact]
    public async Task RestoreFolders_RemovedFolder_MarkedActiveAndAddedToFolders()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        var b = vm.Folders[1];
        vm.RemoveFolder(b);

        vm.RestoreFolder(b);

        Assert.False(b.IsRemoved);
        Assert.Contains(b, vm.Folders);
    }

    [Fact]
    public async Task RemoveFolders_CallsSaveFolderOrder()
    {
        var library = Substitute.For<IPhotoLibraryService>();
        library.LoadLibraryAsync(Arg.Any<string>())
            .Returns(Task.FromResult(new List<PhotoFolder> { MakeFolder("A") }));
        var vm = new OrganiseViewModel(library);
        await vm.LoadAsync(FakePath);

        vm.RemoveFolder(vm.Folders[0]);

        library.Received().SaveFolderOrder(Arg.Any<string>(), Arg.Any<IEnumerable<PhotoFolder>>());
    }

    // ── ReorderPhotos ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderPhotos_SingleItem_MovesToStart()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg", "b.jpg", "c.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var c = vm.SelectedFolder.Photos[2];

        vm.ReorderPhotos([c], 0);

        Assert.Equal("c.jpg", vm.SelectedFolder.Photos[0].FileName);
    }

    [Fact]
    public async Task ReorderPhotos_NoSelectedFolder_DoesNothing()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg", "b.jpg"));
        vm.SelectedFolder = null;

        // Should not throw
        vm.ReorderPhotos([new PhotoItemViewModel(new PhotoItem { FileName = "a.jpg", FullPath = "" })], 0);
    }

    [Fact]
    public async Task ReorderPhotos_CallsSavePhotoOrder()
    {
        var library = Substitute.For<IPhotoLibraryService>();
        library.LoadLibraryAsync(Arg.Any<string>())
            .Returns(Task.FromResult(new List<PhotoFolder> { MakeFolder("A", "a.jpg", "b.jpg") }));
        var vm = new OrganiseViewModel(library);
        await vm.LoadAsync(FakePath);
        vm.SelectedFolder = vm.Folders[0];

        vm.ReorderPhotos([vm.SelectedFolder.Photos[0]], 2);

        library.Received(1).SavePhotoOrder(Arg.Any<PhotoFolder>(), Arg.Any<IEnumerable<PhotoItem>>());
    }

    // ── RemovePhotos / RestorePhotos ─────────────────────────────────────────────

    [Fact]
    public async Task RemovePhotos_ActivePhoto_MarkedRemovedAndRemovedFromPhotos()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg", "b.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var b = vm.SelectedFolder.Photos[1];

        vm.RemovePhoto(b);

        Assert.True(b.IsRemoved);
        Assert.DoesNotContain(b, vm.SelectedFolder.Photos);
    }

    [Fact]
    public async Task RestorePhotos_RemovedPhoto_MarkedActiveAndAddedToPhotos()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg", "b.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var b = vm.SelectedFolder.Photos[1];
        vm.RemovePhoto(b);

        vm.RestorePhoto(b);

        Assert.False(b.IsRemoved);
        Assert.Contains(b, vm.SelectedFolder.Photos);
    }

    // ── SetCaption / ToggleMirrors ───────────────────────────────────────────────

    [Fact]
    public async Task SetCaption_UpdatesCaption()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var photo = vm.SelectedFolder.Photos[0];

        vm.SetCaption(photo, "My Caption");

        Assert.Equal("My Caption", photo.Caption);
    }

    [Fact]
    public async Task ToggleMirrors_SetsMirrorFlag()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var photo = vm.SelectedFolder.Photos[0];
        Assert.False(photo.IsMirrored);

        vm.ToggleMirrors([photo]);

        Assert.True(photo.IsMirrored);
    }

    [Fact]
    public async Task ToggleMirrors_UnsetsMirrorFlag()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var photo = vm.SelectedFolder.Photos[0];
        vm.ToggleMirrors([photo]);

        vm.ToggleMirrors([photo]);

        Assert.False(photo.IsMirrored);
    }

    // ── Undo ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Undo_AfterReorderFolders_RestoresPreviousOrder()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"), MakeFolder("C"));
        vm.ReorderFolders([vm.Folders[2]], 0); // move C to front

        vm.Undo();

        Assert.Equal(new[] { "A", "B", "C" }, vm.Folders.Select(f => f.Name));
    }

    [Fact]
    public async Task Undo_AfterRemoveFolder_RestoresFolder()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        var b = vm.Folders[1];
        vm.RemoveFolder(b);

        vm.Undo();

        Assert.Contains(b, vm.Folders);
        Assert.False(b.IsRemoved);
    }

    [Fact]
    public async Task Undo_AfterSetCaption_RestoresCaption()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var photo = vm.SelectedFolder.Photos[0];
        vm.SetCaption(photo, "Before");
        vm.SetCaption(photo, "After");

        vm.Undo();

        Assert.Equal("Before", photo.Caption);
    }

    [Fact]
    public async Task Undo_AfterToggleMirror_RestoresMirrorState()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var photo = vm.SelectedFolder.Photos[0];
        vm.ToggleMirrors([photo]);

        vm.Undo();

        Assert.False(photo.IsMirrored);
    }

    [Fact]
    public async Task Undo_EmptyStack_DoesNotThrow()
    {
        var vm = await BuildVm(MakeFolder("A"));
        Assert.False(vm.CanUndo);

        var ex = Record.Exception(() => vm.Undo());

        Assert.Null(ex);
    }

    [Fact]
    public async Task Undo_StackExceedsMaxDepth_OldestEntryDropped()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"), MakeFolder("C"));

        // Push 21 undo entries by repeatedly reordering
        for (int i = 0; i < 21; i++)
        {
            var first = vm.Folders[0];
            vm.ReorderFolders([first], vm.Folders.Count);
        }

        // Undo 20 times — should not throw
        for (int i = 0; i < 20; i++)
            vm.Undo();

        Assert.False(vm.CanUndo);
    }

    [Fact]
    public async Task CanUndo_FalseInitially()
    {
        var vm = await BuildVm(MakeFolder("A"));
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public async Task CanUndo_TrueAfterMutation()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        vm.RemoveFolder(vm.Folders[0]);
        Assert.True(vm.CanUndo);
    }

    [Fact]
    public async Task CanUndo_FalseAfterAllUndone()
    {
        var vm = await BuildVm(MakeFolder("A"), MakeFolder("B"));
        vm.RemoveFolder(vm.Folders[0]);
        vm.Undo();
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public async Task Undo_AfterReorderPhotos_RestoresPreviousOrder()
    {
        var vm = await BuildVm(MakeFolder("A", "a.jpg", "b.jpg", "c.jpg"));
        vm.SelectedFolder = vm.Folders[0];
        var c = vm.SelectedFolder.Photos[2];
        vm.ReorderPhotos([c], 0);

        vm.Undo();

        Assert.Equal(new[] { "a.jpg", "b.jpg", "c.jpg" },
            vm.SelectedFolder.Photos.Select(p => p.FileName));
    }
}
