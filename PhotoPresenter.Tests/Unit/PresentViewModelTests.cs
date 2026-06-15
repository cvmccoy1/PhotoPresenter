using System.Windows;
using PhotoPresenter.Models;
using PhotoPresenter.Tests.Infrastructure;
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

    // ---------------------------------------------------------------------------
    // Navigation — CurrentFolder / CurrentPhotoItem update synchronously
    // ---------------------------------------------------------------------------

    [Fact]
    public void NextPhoto_AdvancesPhotoIndex()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3);
        vm.SetFolders(folders, startFolderIndex: 0, startPhotoIndex: 0);

        vm.NextPhoto();

        Assert.Same(folders[0].Photos[1], vm.CurrentPhotoItem);
    }

    [Fact]
    public void NextPhoto_AtLastPhotoOfFolder_MovesToNextFolder()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3, 2);
        vm.SetFolders(folders, startFolderIndex: 0, startPhotoIndex: 2);

        vm.NextPhoto();

        Assert.Same(folders[1], vm.CurrentFolder);
        Assert.Same(folders[1].Photos[0], vm.CurrentPhotoItem);
    }

    [Fact]
    public void NextPhoto_AtLastPhotoOfLastFolder_WrapsToFirstFolder()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3, 2);
        vm.SetFolders(folders, startFolderIndex: 1, startPhotoIndex: 1);

        vm.NextPhoto();

        Assert.Same(folders[0], vm.CurrentFolder);
        Assert.Same(folders[0].Photos[0], vm.CurrentPhotoItem);
    }

    [Fact]
    public void PreviousPhoto_DecreasesPhotoIndex()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3);
        vm.SetFolders(folders, startFolderIndex: 0, startPhotoIndex: 2);

        vm.PreviousPhoto();

        Assert.Same(folders[0].Photos[1], vm.CurrentPhotoItem);
    }

    [Fact]
    public void PreviousPhoto_AtFirstPhotoOfFolder_MovesToPreviousFolder()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3, 2);
        vm.SetFolders(folders, startFolderIndex: 1, startPhotoIndex: 0);

        vm.PreviousPhoto();

        Assert.Same(folders[0], vm.CurrentFolder);
        Assert.Same(folders[0].Photos[2], vm.CurrentPhotoItem);
    }

    [Fact]
    public void PreviousPhoto_AtFirstPhotoOfFirstFolder_WrapsToLastFolder()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3, 2);
        vm.SetFolders(folders, startFolderIndex: 0, startPhotoIndex: 0);

        vm.PreviousPhoto();

        Assert.Same(folders[1], vm.CurrentFolder);
        Assert.Same(folders[1].Photos[1], vm.CurrentPhotoItem);
    }

    // ---------------------------------------------------------------------------
    // CurrentFolder / CurrentPhotoItem
    // ---------------------------------------------------------------------------

    [Fact]
    public void CurrentFolder_ReturnsCurrentFolderViewModel()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(3, 5);
        vm.SetFolders(folders, startFolderIndex: 1);

        Assert.Same(folders[1], vm.CurrentFolder);
    }

    [Fact]
    public void CurrentPhotoItem_ReturnsCurrentPhotoViewModel()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(5);
        vm.SetFolders(folders, startFolderIndex: 0, startPhotoIndex: 2);

        Assert.Same(folders[0].Photos[2], vm.CurrentPhotoItem);
    }

    [Fact]
    public void CurrentFolder_EmptyFolderList_ReturnsNull()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(new List<PhotoFolderViewModel>());

        Assert.Null(vm.CurrentFolder);
    }

    [Fact]
    public void CurrentPhotoItem_EmptyFolderList_ReturnsNull()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(new List<PhotoFolderViewModel>());

        Assert.Null(vm.CurrentPhotoItem);
    }

    // ---------------------------------------------------------------------------
    // Zoom
    // ---------------------------------------------------------------------------

    [Fact]
    public void ZoomIn_IncreasesZoomScale()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));
        var before = vm.ZoomScale;

        vm.ZoomIn();

        Assert.True(vm.ZoomScale > before);
    }

    [Fact]
    public void ZoomOut_DecreasesZoomScale()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));
        vm.ZoomIn();
        var after = vm.ZoomScale;

        vm.ZoomOut();

        Assert.True(vm.ZoomScale < after);
    }

    [Fact]
    public void ZoomIn_CappedAt10()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));
        for (int i = 0; i < 40; i++) vm.ZoomIn();

        Assert.Equal(10.0, vm.ZoomScale);
    }

    [Fact]
    public void ZoomOut_CappedAtHalf()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));
        for (int i = 0; i < 30; i++) vm.ZoomOut();

        Assert.Equal(0.5, vm.ZoomScale);
    }

    [Fact]
    public void ZoomByDelta_Positive_ZoomsIn()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));
        var before = vm.ZoomScale;

        vm.ZoomByDelta(120);

        Assert.True(vm.ZoomScale > before);
    }

    [Fact]
    public void ZoomByDelta_Negative_ZoomsOut()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));
        vm.ZoomIn();
        var after = vm.ZoomScale;

        vm.ZoomByDelta(-120);

        Assert.True(vm.ZoomScale < after);
    }

    // ---------------------------------------------------------------------------
    // Pan
    // ---------------------------------------------------------------------------

    [Fact]
    public void BeginPan_ThenUpdatePan_SetsPanOffset()
    {
        var vm = new PresentViewModel();
        vm.SetFolders(MakeFolders(1));

        vm.BeginPan(new Point(100, 80));
        vm.UpdatePan(new Point(150, 110));

        Assert.Equal(50.0, vm.PanX);
        Assert.Equal(30.0, vm.PanY);
    }

    [Fact]
    public void SetFolders_ResetsPanToZero()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(2);
        vm.SetFolders(folders);
        vm.BeginPan(new Point(0, 0));
        vm.UpdatePan(new Point(50, 50));

        vm.SetFolders(folders);

        Assert.Equal(0.0, vm.PanX);
        Assert.Equal(0.0, vm.PanY);
    }

    // ---------------------------------------------------------------------------
    // Video rotation
    // ---------------------------------------------------------------------------

    [Fact]
    public void RotateVideo_IncreasesByNinety()
    {
        var vm = new PresentViewModel();
        Assert.Equal(0.0, vm.VideoRotation);

        vm.RotateVideo();

        Assert.Equal(90.0, vm.VideoRotation);
    }

    [Fact]
    public void RotateVideo_WrapsAfterFourTurns()
    {
        var vm = new PresentViewModel();
        for (int i = 0; i < 4; i++) vm.RotateVideo();

        Assert.Equal(0.0, vm.VideoRotation);
    }

    // ---------------------------------------------------------------------------
    // SetFolders resets
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetFolders_ResetsZoomToOne()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(2);
        vm.SetFolders(folders);
        vm.ZoomIn();
        Assert.True(vm.ZoomScale > 1.0);

        vm.SetFolders(folders);

        Assert.Equal(1.0, vm.ZoomScale);
    }

    [Fact]
    public void SetFolders_ResetsVideoRotationToZero()
    {
        var vm = new PresentViewModel();
        var folders = MakeFolders(2);
        vm.SetFolders(folders);
        vm.RotateVideo();
        Assert.Equal(90.0, vm.VideoRotation);

        vm.SetFolders(folders);

        Assert.Equal(0.0, vm.VideoRotation);
    }

    // ---------------------------------------------------------------------------
    // Computed properties
    // ---------------------------------------------------------------------------

    [Fact]
    public void MirrorScaleX_WhenNotMirrored_ReturnsOne()
    {
        var vm = new PresentViewModel();
        vm.CurrentIsMirrored = false;

        Assert.Equal(1.0, vm.MirrorScaleX);
    }

    [Fact]
    public void MirrorScaleX_WhenMirrored_ReturnsNegativeOne()
    {
        var vm = new PresentViewModel();
        vm.CurrentIsMirrored = true;

        Assert.Equal(-1.0, vm.MirrorScaleX);
    }

    [Fact]
    public void HasCurrentCaption_EmptyString_ReturnsFalse()
    {
        var vm = new PresentViewModel();
        vm.CurrentCaption = "";

        Assert.False(vm.HasCurrentCaption);
    }

    [Fact]
    public void HasCurrentCaption_WithText_ReturnsTrue()
    {
        var vm = new PresentViewModel();
        vm.CurrentCaption = "Summer 2025";

        Assert.True(vm.HasCurrentCaption);
    }

    [Fact]
    public void PlayPauseIcon_WhenNotPlaying_ShowsPlaySymbol()
    {
        var vm = new PresentViewModel();
        vm.IsPlaying = false;

        Assert.Equal("▶", vm.PlayPauseIcon);
    }

    [Fact]
    public void PlayPauseIcon_WhenPlaying_ShowsPauseSymbol()
    {
        var vm = new PresentViewModel();
        vm.IsPlaying = true;

        Assert.Equal("⏸", vm.PlayPauseIcon);
    }

    // ---------------------------------------------------------------------------
    // Time formatting
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdatePosition_SubHourDuration_FormatsMinutesAndSeconds()
    {
        var vm = new PresentViewModel();
        vm.UpdatePosition(TimeSpan.FromSeconds(23), TimeSpan.FromSeconds(196));

        Assert.Equal("0:23 / 3:16", vm.PositionLabel);
    }

    [Fact]
    public void UpdatePosition_OverHourDuration_IncludesHours()
    {
        var vm = new PresentViewModel();
        vm.UpdatePosition(TimeSpan.FromSeconds(3661), TimeSpan.FromSeconds(7200));

        Assert.Equal("1:01:01 / 2:00:00", vm.PositionLabel);
    }

    [Fact]
    public void UpdatePosition_ZeroDuration_FormatsAsZero()
    {
        var vm = new PresentViewModel();
        vm.UpdatePosition(TimeSpan.Zero, TimeSpan.Zero);

        Assert.Equal("0:00 / 0:00", vm.PositionLabel);
    }

    // ── Real-file async loading (covers LoadCurrentPhotoAsync success path) ────────

    [Fact]
    public async Task SetFolders_WithRealJpegOnDisk_LoadsCurrentImageAsync()
    {
        using var tmp = new TempDirectory();
        var path = tmp.CreateFile("img.jpg", TempDirectory.TinyJpegBytes);
        var folder = new PhotoFolder { Name = "F", FullPath = tmp.Path };
        folder.Photos.Add(new PhotoItem { FileName = "img.jpg", FullPath = path });

        var vm = new PresentViewModel();
        var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PresentViewModel.CurrentImage) && vm.CurrentImage != null)
                loaded.TrySetResult(true);
        };

        vm.SetFolders(new List<PhotoFolderViewModel> { new PhotoFolderViewModel(folder) });
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(vm.CurrentImage);
        Assert.NotEmpty(vm.FolderLabel);
        Assert.NotEmpty(vm.PhotoLabel);
        Assert.NotEmpty(vm.OverallLabel);
    }

    [Fact]
    public async Task NextPhoto_WithTwoRealJpegs_LoadsBothImagesSuccessfully()
    {
        using var tmp = new TempDirectory();
        var path1 = tmp.CreateFile("img1.jpg", TempDirectory.TinyJpegBytes);
        var path2 = tmp.CreateFile("img2.jpg", TempDirectory.TinyJpegBytes);
        var folder = new PhotoFolder { Name = "F", FullPath = tmp.Path };
        folder.Photos.Add(new PhotoItem { FileName = "img1.jpg", FullPath = path1 });
        folder.Photos.Add(new PhotoItem { FileName = "img2.jpg", FullPath = path2 });

        var vm = new PresentViewModel();

        // Stage 1: wait for the first photo to load.
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PresentViewModel.CurrentImage) && vm.CurrentImage != null)
                first.TrySetResult(true);
        };
        vm.SetFolders(new List<PhotoFolderViewModel> { new PhotoFolderViewModel(folder) });
        await first.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Stage 2: navigate to photo2 and wait for it to load.
        // PreloadNextAsync runs after photo1 loads (lines 272-282); NextPhoto either uses
        // the preloaded image (lines 204-219) or falls back to a fresh load (222-232).
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PresentViewModel.CurrentImage) && vm.CurrentImage != null)
                second.TrySetResult(true);
        };
        vm.NextPhoto();
        await second.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(vm.CurrentImage);
    }

    [Fact]
    public async Task NextPhoto_WhenPreloadAlreadyComplete_UsesPreloadedImageFastPath()
    {
        // This test explicitly waits for PreloadNextAsync to finish before calling
        // NextPhoto, so the preloaded-image fast path (lines 204-219) is exercised.
        using var tmp = new TempDirectory();
        var path1 = tmp.CreateFile("pre1.jpg", TempDirectory.TinyJpegBytes);
        var path2 = tmp.CreateFile("pre2.jpg", TempDirectory.TinyJpegBytes);
        var folder = new PhotoFolder { Name = "F", FullPath = tmp.Path };
        folder.Photos.Add(new PhotoItem { FileName = "pre1.jpg", FullPath = path1 });
        folder.Photos.Add(new PhotoItem { FileName = "pre2.jpg", FullPath = path2 });

        var vm = new PresentViewModel();

        // Stage 1: wait for photo1 to load.
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PresentViewModel.CurrentImage) && vm.CurrentImage != null)
                first.TrySetResult(true);
        };
        vm.SetFolders(new List<PhotoFolderViewModel> { new PhotoFolderViewModel(folder) });
        await first.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Allow PreloadNextAsync (fire-and-forget after photo1 loads) to finish loading
        // photo2 into the internal cache before we navigate.
        await Task.Delay(500);

        // Stage 2: navigate — should use the preloaded cache (fast path, no Task.Run).
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PresentViewModel.CurrentImage) && vm.CurrentImage != null)
                second.TrySetResult(true);
        };
        vm.NextPhoto();
        await second.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(vm.CurrentImage);
    }
}
