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
}
