using PhotoPresenter.Models;
using PhotoPresenter.ViewModels;
using Xunit;

namespace PhotoPresenter.Tests.Unit;

public class PresentViewModelTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static PhotoFolderViewModel FolderWithPhotos(int index, int count)
    {
        var model = new PhotoFolder
        {
            Name = $"Folder{index}",
            FullPath = $@"Z:\Folder{index}"
        };
        for (int i = 0; i < count; i++)
            model.Photos.Add(new PhotoItem
            {
                FileName = $"photo{i}.jpg",
                FullPath = $@"Z:\Folder{index}\photo{i}.jpg"
            });
        return new PhotoFolderViewModel(model);
    }

    private static List<PhotoFolderViewModel> MakeFolders(params int[] photoCounts)
    {
        var folders = new List<PhotoFolderViewModel>();
        for (int i = 0; i < photoCounts.Length; i++)
            folders.Add(FolderWithPhotos(i, photoCounts[i]));
        return folders;
    }

    // ---------------------------------------------------------------------------
    // OverallLabel — cumulative-count correctness
    // ---------------------------------------------------------------------------

    [Fact]
    public void OverallLabel_FirstItemOfFirstFolder_ShowsOneOfTotal()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 0, startPhotoIndex: 0);

        Assert.Equal("1 of 10", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_LastItemOfFirstFolder_ShowsThreeOfTotal()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 0, startPhotoIndex: 2);

        Assert.Equal("3 of 10", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_FirstItemOfSecondFolder_AddsFirstFolderCount()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 1, startPhotoIndex: 0);

        // cumulative offset for folder 1 = 3; photo 0 → position 4
        Assert.Equal("4 of 10", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_MiddleOfSecondFolder_CountsCorrectly()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 1, startPhotoIndex: 3);

        // cumulative 3 + photo index 3 + 1 = 7
        Assert.Equal("7 of 10", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_LastItemOfLastFolder_ShowsTotalOfTotal()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 2, startPhotoIndex: 1);

        Assert.Equal("10 of 10", vm.OverallLabel);
    }

    // ---------------------------------------------------------------------------
    // OverallLabel — edge cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void OverallLabel_SingleFolder_CountsWithinThatFolder()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(7), startFolderIndex: 0, startPhotoIndex: 4);

        Assert.Equal("5 of 7", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_EmptyFolderList_RemainsEmpty()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(new List<PhotoFolderViewModel>());

        Assert.Equal("", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_OutOfRangeStartFolder_ClampedToLastFolder()
    {
        var vm = new PresentViewModel();
        // startFolderIndex=99 should clamp to 2 (last folder); startPhotoIndex=99 clamps to 1
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 99, startPhotoIndex: 99);

        // clamped to folder 2 (offset 8), photo 1 → position 10
        Assert.Equal("10 of 10", vm.OverallLabel);
    }

    [Fact]
    public void OverallLabel_OutOfRangeStartPhoto_ClampedToLastPhoto()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5, 2), startFolderIndex: 0, startPhotoIndex: 99);

        // clamped to folder 0, photo 2 → position 3
        Assert.Equal("3 of 10", vm.OverallLabel);
    }

    // ---------------------------------------------------------------------------
    // FolderLabel and PhotoLabel — format sanity
    // ---------------------------------------------------------------------------

    [Fact]
    public void FolderLabel_ShowsFolderNameAndIndexOfTotal()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(3, 5), startFolderIndex: 1, startPhotoIndex: 0);

        Assert.Contains("Folder1", vm.FolderLabel);
        Assert.Contains("2 of 2", vm.FolderLabel);
    }

    [Fact]
    public void PhotoLabel_ShowsItemIndexOfFolderCount()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(5), startFolderIndex: 0, startPhotoIndex: 2);

        Assert.Equal("Item 3 of 5", vm.PhotoLabel);
    }
}
