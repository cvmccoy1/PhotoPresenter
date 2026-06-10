using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoPresenter.Tests.Infrastructure;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Unit;

public class ExifOrientationTests
{
    // ── ReadOrientationFromMetadata — no STA needed ──────────────────────────────

    [Fact]
    public void ReadOrientationFromMetadata_NullMetadata_Returns1()
    {
        Assert.Equal(1, PhotoItemViewModel.ReadOrientationFromMetadata(null));
    }

    // ── ApplyExifOrientation — STA required ──────────────────────────────────────
    // Source is 4 wide × 2 tall (Gray8). Orientations that rotate 90°/270° swap dimensions.

    [StaTheory]
    [InlineData(1, 4, 2)]   // no-op
    [InlineData(2, 4, 2)]   // horizontal flip → same dimensions
    [InlineData(3, 4, 2)]   // 180° → same dimensions
    [InlineData(4, 4, 2)]   // vertical flip → same dimensions
    [InlineData(5, 2, 4)]   // 90° + flip → swapped
    [InlineData(6, 2, 4)]   // 90° CW → swapped
    [InlineData(7, 2, 4)]   // 270° + flip → swapped
    [InlineData(8, 2, 4)]   // 270° CW → swapped
    public void ApplyExifOrientation_DimensionsCorrect(int orientation, int expectedW, int expectedH)
    {
        var src = MakeSource(4, 2);
        var result = PhotoItemViewModel.ApplyExifOrientation(src, orientation);
        Assert.Equal(expectedW, result.PixelWidth);
        Assert.Equal(expectedH, result.PixelHeight);
    }

    [StaFact]
    public void ApplyExifOrientation_Orientation1_ReturnsSameSource()
    {
        var src = MakeSource(4, 2);
        var result = PhotoItemViewModel.ApplyExifOrientation(src, 1);
        Assert.Same(src, result);
    }

    [StaTheory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void ApplyExifOrientation_OutOfRange_ReturnsSameSource(int orientation)
    {
        var src = MakeSource(4, 2);
        var result = PhotoItemViewModel.ApplyExifOrientation(src, orientation);
        Assert.Same(src, result);
    }

    [StaFact]
    public void ApplyExifOrientation_Orientation3_PixelValuesRotated180()
    {
        // 4×1 source: [10, 20, 30, 40] — after 180° flip → [40, 30, 20, 10]
        var pixels = new byte[] { 10, 20, 30, 40 };
        var src = BitmapSource.Create(4, 1, 96, 96, PixelFormats.Gray8, null, pixels, 4);
        src.Freeze();

        var result = PhotoItemViewModel.ApplyExifOrientation(src, 3);

        var output = new byte[4];
        result.CopyPixels(output, 4, 0);
        Assert.Equal(new byte[] { 40, 30, 20, 10 }, output);
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    private static BitmapSource MakeSource(int w, int h)
    {
        var pixels = new byte[w * h];
        var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, pixels, w);
        src.Freeze();
        return src;
    }
}
