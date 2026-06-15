using PhotoPresenter.Models;
using PhotoPresenter.ViewModels;
using Xunit;

namespace PhotoPresenter.Tests.Unit;

public class PhotoFolderViewModelTests
{
    private static PhotoFolder MakeFolder(string name, params (string fileName, bool isVideo)[] items)
    {
        var folder = new PhotoFolder { Name = name, FullPath = $@"Z:\{name}" };
        foreach (var (fileName, isVideo) in items)
            folder.Photos.Add(new PhotoItem
            {
                FileName = fileName,
                FullPath = $@"Z:\{name}\{fileName}",
                IsVideo  = isVideo
            });
        return folder;
    }

    // ---------------------------------------------------------------------------
    // FolderToolTipText
    // ---------------------------------------------------------------------------

    [Fact]
    public void FolderToolTipText_PhotosOnly_ShowsCorrectCounts()
    {
        var vm = new PhotoFolderViewModel(MakeFolder("F",
            ("a.jpg", false), ("b.jpg", false), ("c.jpg", false)));

        Assert.Contains("Photos: 3", vm.FolderToolTipText);
        Assert.Contains("Videos: 0", vm.FolderToolTipText);
        Assert.Contains("Total:  3", vm.FolderToolTipText);
    }

    [Fact]
    public void FolderToolTipText_VideosOnly_ShowsCorrectCounts()
    {
        var vm = new PhotoFolderViewModel(MakeFolder("F",
            ("a.mp4", true), ("b.mp4", true)));

        Assert.Contains("Photos: 0", vm.FolderToolTipText);
        Assert.Contains("Videos: 2", vm.FolderToolTipText);
        Assert.Contains("Total:  2", vm.FolderToolTipText);
    }

    [Fact]
    public void FolderToolTipText_Mixed_ShowsCorrectCounts()
    {
        var vm = new PhotoFolderViewModel(MakeFolder("F",
            ("a.jpg", false), ("b.mp4", true), ("c.jpg", false)));

        Assert.Contains("Photos: 2", vm.FolderToolTipText);
        Assert.Contains("Videos: 1", vm.FolderToolTipText);
        Assert.Contains("Total:  3", vm.FolderToolTipText);
    }

    [Fact]
    public void FolderToolTipText_Empty_ShowsZeroCounts()
    {
        var vm = new PhotoFolderViewModel(
            new PhotoFolder { Name = "Empty", FullPath = @"Z:\Empty" });

        Assert.Contains("Photos: 0", vm.FolderToolTipText);
        Assert.Contains("Videos: 0", vm.FolderToolTipText);
        Assert.Contains("Total:  0", vm.FolderToolTipText);
    }

    // ---------------------------------------------------------------------------
    // UpdatePath
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdatePath_UpdatesFolderNameAndFullPath()
    {
        var vm = new PhotoFolderViewModel(
            new PhotoFolder { Name = "OldName", FullPath = @"Z:\OldName" });

        vm.UpdatePath("NewName", @"Z:\NewName");

        Assert.Equal("NewName", vm.Name);
        Assert.Equal(@"Z:\NewName", vm.FullPath);
    }

    [Fact]
    public void UpdatePath_CascadesFullPathToChildPhotos()
    {
        var vm = new PhotoFolderViewModel(MakeFolder("Old",
            ("photo1.jpg", false), ("photo2.jpg", false)));

        vm.UpdatePath("New", @"Z:\New");

        Assert.Equal(@"Z:\New\photo1.jpg", vm.AllPhotoItems[0].FullPath);
        Assert.Equal(@"Z:\New\photo2.jpg", vm.AllPhotoItems[1].FullPath);
    }

    [Fact]
    public void UpdatePath_PreservesChildFileNames()
    {
        var vm = new PhotoFolderViewModel(MakeFolder("Old", ("img.jpg", false)));

        vm.UpdatePath("New", @"Z:\New");

        Assert.Equal("img.jpg", vm.AllPhotoItems[0].FileName);
    }

    [Fact]
    public void UpdatePath_SyncsChangesToModel()
    {
        var folder = new PhotoFolder { Name = "OldName", FullPath = @"Z:\OldName" };
        var vm = new PhotoFolderViewModel(folder);

        vm.UpdatePath("NewName", @"Z:\NewName");

        Assert.Equal("NewName", folder.Name);
        Assert.Equal(@"Z:\NewName", folder.FullPath);
    }

    // ---------------------------------------------------------------------------
    // Constructor — photo separation
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_ActivePhotos_AddedToBothCollections()
    {
        var vm = new PhotoFolderViewModel(MakeFolder("F",
            ("a.jpg", false), ("b.jpg", false)));

        Assert.Equal(2, vm.Photos.Count);
        Assert.Equal(2, vm.AllPhotoItems.Count);
    }

    [Fact]
    public void Constructor_RemovedPhoto_InAllItemsButNotInPhotos()
    {
        var folder = new PhotoFolder { Name = "F", FullPath = @"Z:\F" };
        folder.Photos.Add(new PhotoItem { FileName = "a.jpg", FullPath = @"Z:\F\a.jpg", IsRemoved = false });
        folder.Photos.Add(new PhotoItem { FileName = "b.jpg", FullPath = @"Z:\F\b.jpg", IsRemoved = true });

        var vm = new PhotoFolderViewModel(folder);

        Assert.Single(vm.Photos);
        Assert.Equal(2, vm.AllPhotoItems.Count);
        Assert.Equal("a.jpg", vm.Photos[0].FileName);
    }

    [Fact]
    public void Constructor_IsRemoved_MatchesModel()
    {
        var folder = new PhotoFolder { Name = "F", FullPath = @"Z:\F", IsRemoved = true };

        var vm = new PhotoFolderViewModel(folder);

        Assert.True(vm.IsRemoved);
    }
}
