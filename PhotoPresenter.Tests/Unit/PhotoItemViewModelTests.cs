using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoPresenter.Models;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Unit;

public class PhotoItemViewModelTests
{
    private static PhotoItemViewModel MakeVm(string fileName = "test.jpg", string? fullPath = null)
    {
        var path = fullPath ?? $@"Z:\nonexistent\{fileName}";
        return new PhotoItemViewModel(new PhotoItem { FileName = fileName, FullPath = path });
    }

    // Must be called from an STA thread.
    private static BitmapSource TinyBitmap()
    {
        var bmp = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[4], 4);
        bmp.Freeze();
        return bmp;
    }

    // ── HasThumbnail ─────────────────────────────────────────────────────────────

    [Fact]
    public void HasThumbnail_FalseByDefault()
    {
        var vm = MakeVm();
        Assert.False(vm.HasThumbnail);
        Assert.Null(vm.Thumbnail);
    }

    [StaFact]
    public void HasThumbnail_TrueAfterThumbnailAssigned()
    {
        var vm = MakeVm();
        vm.Thumbnail = TinyBitmap();
        Assert.True(vm.HasThumbnail);
    }

    // ── EnsureThumbnailLoaded ────────────────────────────────────────────────────

    [Fact]
    public void EnsureThumbnailLoaded_DoesNotThrow_ForNonExistentFile()
    {
        var vm = MakeVm();
        Assert.Null(Record.Exception(() => vm.EnsureThumbnailLoaded()));
    }

    [Fact]
    public void EnsureThumbnailLoaded_IsIdempotent_SecondCallDoesNotThrow()
    {
        // _thumbnailRequested is set on the first call; the second call must be a no-op.
        var vm = MakeVm();
        vm.EnsureThumbnailLoaded();
        Assert.Null(Record.Exception(() => vm.EnsureThumbnailLoaded()));
    }

    [StaFact]
    public void EnsureThumbnailLoaded_WhenThumbnailAlreadySet_LeavesItUnchanged()
    {
        // Thumbnail != null causes early return; the existing value must be preserved.
        var vm = MakeVm();
        var bmp = TinyBitmap();
        vm.Thumbnail = bmp;

        vm.EnsureThumbnailLoaded();

        Assert.Same(bmp, vm.Thumbnail);
    }

    // ── RetryThumbnailAfterDelayAsync ────────────────────────────────────────────

    [Fact]
    public async Task RetryThumbnailAfterDelayAsync_StillPendingAfterShortWait_WhenThumbnailNull()
    {
        // The first iteration waits 2 000 ms before any check — the task must not
        // have completed 200 ms after it starts.
        // The background task eventually exhausts all retries (~35 s total) with no
        // side-effects: the file does not exist so LoadThumbnailAsync returns early
        // without touching the semaphore.
        var vm = MakeVm();

        var retryTask = vm.RetryThumbnailAfterDelayAsync();
        var winner = await Task.WhenAny(retryTask, Task.Delay(200));

        Assert.NotSame(retryTask, winner);
    }

    [StaFact]
    public async Task RetryThumbnailAfterDelayAsync_ExitsAfterFirstDelay_WhenThumbnailAlreadySet()
    {
        // After the first 2-second delay the loop checks Thumbnail; finding it
        // non-null it returns immediately.  Allow 4 seconds of headroom.
        // This test intentionally takes ~2 seconds to exercise the early-exit path.
        var vm = MakeVm();
        vm.Thumbnail = TinyBitmap();

        var retryTask = vm.RetryThumbnailAfterDelayAsync();
        var winner = await Task.WhenAny(retryTask, Task.Delay(TimeSpan.FromSeconds(4)));

        Assert.Same(retryTask, winner); // completed, not timed out
    }

    // ── UpdatePath ───────────────────────────────────────────────────────────────

    [Fact]
    public void UpdatePath_UpdatesViewModelProperties()
    {
        var vm = MakeVm("old.jpg", @"Z:\fake\old.jpg");
        vm.UpdatePath("new.jpg", @"Z:\fake\new.jpg");
        Assert.Equal("new.jpg", vm.FileName);
        Assert.Equal(@"Z:\fake\new.jpg", vm.FullPath);
    }

    [Fact]
    public void UpdatePath_SyncsChangesToModel()
    {
        var vm = MakeVm("old.jpg", @"Z:\fake\old.jpg");
        vm.UpdatePath("new.jpg", @"Z:\fake\new.jpg");
        Assert.Equal("new.jpg", vm.Model.FileName);
        Assert.Equal(@"Z:\fake\new.jpg", vm.Model.FullPath);
    }

    [Fact]
    public void UpdatePath_DoesNotAffectOtherProperties()
    {
        var vm = MakeVm("old.jpg", @"Z:\fake\old.jpg");
        vm.Caption = "Keep this";
        vm.IsMirrored = true;

        vm.UpdatePath("new.jpg", @"Z:\fake\new.jpg");

        Assert.Equal("Keep this", vm.Caption);
        Assert.True(vm.IsMirrored);
    }

    // ── HasCaption ────────────────────────────────────────────────────────────────

    [Fact]
    public void HasCaption_FalseWhenCaptionIsEmpty()
    {
        var vm = MakeVm();
        vm.Caption = "";
        Assert.False(vm.HasCaption);
    }

    [Fact]
    public void HasCaption_TrueWhenCaptionHasText()
    {
        var vm = MakeVm();
        vm.Caption = "Hello";
        Assert.True(vm.HasCaption);
    }

    [Fact]
    public void Caption_SyncedToModel()
    {
        var vm = MakeVm();
        vm.Caption = "Test";
        Assert.Equal("Test", vm.Model.Caption);
    }

    [Fact]
    public void IsMirrored_SyncedToModel()
    {
        var vm = MakeVm();
        vm.IsMirrored = true;
        Assert.True(vm.Model.IsMirrored);
    }

    // ── EnsureToolTipLoaded ───────────────────────────────────────────────────────

    [Fact]
    public void EnsureToolTipLoaded_SetsToolTipTextSynchronously()
    {
        var item = new PhotoItem
        {
            FileName = "photo.jpg",
            FullPath = @"Z:\nonexistent\photo.jpg",
            CreationDate = new DateTime(2024, 6, 15, 10, 30, 0)
        };
        var vm = new PhotoItemViewModel(item);

        vm.EnsureToolTipLoaded();

        Assert.False(string.IsNullOrEmpty(vm.ToolTipText));
    }

    [Fact]
    public void EnsureToolTipLoaded_IncludesFileExtension()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg" };
        var vm = new PhotoItemViewModel(item);

        vm.EnsureToolTipLoaded();

        Assert.Contains(".JPG", vm.ToolTipText);
    }

    [Fact]
    public void EnsureToolTipLoaded_IncludesDateWhenSet()
    {
        var item = new PhotoItem
        {
            FileName = "photo.jpg",
            FullPath = @"Z:\nonexistent\photo.jpg",
            CreationDate = new DateTime(2024, 3, 7)
        };
        var vm = new PhotoItemViewModel(item);

        vm.EnsureToolTipLoaded();

        Assert.Contains("2024-03-07", vm.ToolTipText);
    }

    [Fact]
    public void EnsureToolTipLoaded_UnknownDateWhenCreationDateDefault()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg" };
        var vm = new PhotoItemViewModel(item);

        vm.EnsureToolTipLoaded();

        Assert.Contains("Unknown", vm.ToolTipText);
    }

    [Fact]
    public void EnsureToolTipLoaded_WhenMirrored_ContainsMirroredLabel()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg", IsMirrored = true };
        var vm = new PhotoItemViewModel(item);

        vm.EnsureToolTipLoaded();

        Assert.Contains("Mirrored: Yes", vm.ToolTipText);
    }

    [Fact]
    public void EnsureToolTipLoaded_WhenNotMirrored_DoesNotContainMirroredLabel()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg", IsMirrored = false };
        var vm = new PhotoItemViewModel(item);

        vm.EnsureToolTipLoaded();

        Assert.DoesNotContain("Mirrored", vm.ToolTipText);
    }

    [Fact]
    public void EnsureToolTipLoaded_IsIdempotent_SecondCallDoesNotReloadText()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg" };
        var vm = new PhotoItemViewModel(item);
        vm.EnsureToolTipLoaded();
        var first = vm.ToolTipText;

        vm.EnsureToolTipLoaded(); // should be a no-op

        Assert.Equal(first, vm.ToolTipText);
    }

    [Fact]
    public void IsMirroredChanged_ResetsToolTipSoNextLoadReflectsMirrorState()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg" };
        var vm = new PhotoItemViewModel(item);
        vm.EnsureToolTipLoaded();
        Assert.DoesNotContain("Mirrored", vm.ToolTipText);

        // Changing IsMirrored resets _toolTipLoaded, so the next call reloads.
        vm.IsMirrored = true;
        vm.EnsureToolTipLoaded();

        Assert.Contains("Mirrored: Yes", vm.ToolTipText);
    }

    // ── FormatSize ────────────────────────────────────────────────────────────────

    [Fact]
    public void FormatSize_Zero_ReturnsUnknown()
        => Assert.Equal("Unknown", PhotoItemViewModel.FormatSize(0));

    [Fact]
    public void FormatSize_Negative_ReturnsUnknown()
        => Assert.Equal("Unknown", PhotoItemViewModel.FormatSize(-1));

    [Fact]
    public void FormatSize_Megabytes_FormatsMB()
    {
        // 5 MB = 5 * 1_048_576 bytes
        var result = PhotoItemViewModel.FormatSize(5 * 1_048_576L);
        Assert.Contains("MB", result);
        Assert.Contains("5", result);
    }

    [Fact]
    public void FormatSize_Gigabytes_FormatsGB()
    {
        // 2 GB = 2 * 1_073_741_824 bytes
        var result = PhotoItemViewModel.FormatSize(2 * 1_073_741_824L);
        Assert.Contains("GB", result);
        Assert.Contains("2", result);
    }

    [Fact]
    public void FormatSize_ExactlyOneGigabyte_FormatsGB()
    {
        var result = PhotoItemViewModel.FormatSize(1_073_741_824L);
        Assert.Contains("GB", result);
    }

    // ── LoadToolTipDetailAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadToolTipDetailAsync_ForPhotoItem_SetsDimensionsInToolTip()
    {
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = @"Z:\nonexistent\photo.jpg" };
        var vm   = new PhotoItemViewModel(item);
        var fi   = new FileInfo(item.FullPath); // doesn't exist → fi.Exists = false

        await vm.LoadToolTipDetailAsync(fi);

        // GetPhotoDimensions catches the IOException for non-existent file → "Unknown"
        Assert.Contains("Dimensions:", vm.ToolTipText);
    }

    [Fact]
    public async Task LoadToolTipDetailAsync_ForVideoItem_SetsLengthInToolTip()
    {
        var item = new PhotoItem { FileName = "clip.mp4", FullPath = @"Z:\nonexistent\clip.mp4", IsVideo = true };
        var vm   = new PhotoItemViewModel(item);
        var fi   = new FileInfo(item.FullPath);

        await vm.LoadToolTipDetailAsync(fi);

        // GetVideoDuration returns "Unknown" for a non-existent file
        Assert.Contains("Length:", vm.ToolTipText);
    }

    [Fact]
    public async Task LoadToolTipDetailAsync_FileExists_IncludesSizeInToolTip()
    {
        using var tmp = new PhotoPresenter.Tests.Infrastructure.TempDirectory();
        var path = tmp.CreateFile("photo.jpg", PhotoPresenter.Tests.Infrastructure.TempDirectory.TinyJpegBytes);
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = path };
        var vm   = new PhotoItemViewModel(item);
        var fi   = new FileInfo(path);

        await vm.LoadToolTipDetailAsync(fi);

        // fi.Exists = true; FormatSize(fi.Length) → "x.x MB" (not "Unknown")
        Assert.Contains("Size:", vm.ToolTipText);
        Assert.DoesNotContain("Size: Unknown", vm.ToolTipText);
    }

    // ── ReadOrientationFromMetadata — ushort and string branches ─────────────────

    [StaFact]
    public void ReadOrientationFromMetadata_UshortOrientation_ReturnsIntValue()
    {
        var meta = new BitmapMetadata("jpg");
        meta.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);

        int result = PhotoItemViewModel.ReadOrientationFromMetadata(meta);

        Assert.Equal(6, result);
    }

    [StaFact]
    public void ReadOrientationFromMetadata_StringOrientation_ParsesAndReturnsValue()
    {
        var meta = new BitmapMetadata("jpg");
        // XMP stores orientation as a string; the second GetQuery path handles this.
        meta.SetQuery("/xmp/exif:Orientation", "8");

        int result = PhotoItemViewModel.ReadOrientationFromMetadata(meta);

        Assert.Equal(8, result);
    }
}
