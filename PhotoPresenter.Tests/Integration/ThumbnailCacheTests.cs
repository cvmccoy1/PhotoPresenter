using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoPresenter.Services;

namespace PhotoPresenter.Tests.Integration;

/// <summary>
/// Integration tests for ThumbnailCache.  BitmapSource creation and BitmapImage loading
/// both require STA, so all tests run via the shared WPF dispatcher fixture.
/// The cache writes to the real %LOCALAPPDATA%\PhotoPresenter\thumbcache\ directory —
/// these tests are intentionally integration-level and leave real cache files behind.
/// </summary>
[Collection("WPF")]
public class ThumbnailCacheTests
{
    private readonly WpfApplicationFixture _fixture;

    public ThumbnailCacheTests(WpfApplicationFixture fixture) => _fixture = fixture;

    private static BitmapSource MakeBitmap()
    {
        var bmp = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgr32, null, new byte[4 * 4 * 4], 4 * 4);
        bmp.Freeze();
        return bmp;
    }

    // ── TryGet ────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_ForPathWithNoCachedEntry_ReturnsNull()
    {
        _fixture.Invoke(() =>
        {
            // Use a unique path that will never have been cached.
            var result = ThumbnailCache.TryGet($@"Z:\never-cached-{Guid.NewGuid()}.jpg");
            Assert.Null(result);
        });
    }

    // ── Save → TryGet round-trip ──────────────────────────────────────────────────

    [Fact]
    public void Save_ThenTryGet_ReturnsBitmapSource()
    {
        _fixture.Invoke(() =>
        {
            var src = $@"Z:\roundtrip-{Guid.NewGuid()}.jpg";
            ThumbnailCache.Save(src, MakeBitmap());

            var result = ThumbnailCache.TryGet(src);

            Assert.NotNull(result);
        });
    }

    [Fact]
    public void Save_ThenTryGet_ReturnsFrozenBitmap()
    {
        _fixture.Invoke(() =>
        {
            var src = $@"Z:\frozen-{Guid.NewGuid()}.jpg";
            ThumbnailCache.Save(src, MakeBitmap());

            var result = ThumbnailCache.TryGet(src);

            Assert.NotNull(result);
            Assert.True(result!.IsFrozen);
        });
    }

    [Fact]
    public void Save_TwiceForSamePath_SecondTryGetReturnsNonNull()
    {
        _fixture.Invoke(() =>
        {
            var src = $@"Z:\overwrite-{Guid.NewGuid()}.jpg";
            ThumbnailCache.Save(src, MakeBitmap());
            ThumbnailCache.Save(src, MakeBitmap()); // overwrite

            var result = ThumbnailCache.TryGet(src);

            Assert.NotNull(result);
        });
    }

    // ── Shutdown ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Shutdown_CalledMultipleTimes_DoesNotThrow()
    {
        // Shutdown is idempotent — CancellationTokenSource.Cancel() is safe to call twice.
        Assert.Null(Record.Exception(() =>
        {
            ThumbnailCache.Shutdown();
            ThumbnailCache.Shutdown();
        }));
    }
}
