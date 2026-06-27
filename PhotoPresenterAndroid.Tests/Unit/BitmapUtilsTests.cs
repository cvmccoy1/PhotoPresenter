using PhotoPresenterAndroid.Services;

namespace PhotoPresenterAndroid.Tests.Unit;

public class BitmapUtilsTests
{
    // Returns 1 when no downscaling is needed.
    [Theory]
    [InlineData(300, 300)]  // equal
    [InlineData(200, 300)]  // source smaller than target
    [InlineData(1,   300)]  // tiny source
    public void CalculateInSampleSize_Returns1_WhenNoDownscaleNeeded(int source, int target)
    {
        Assert.Equal(1, BitmapUtils.CalculateInSampleSize(source, target));
    }

    [Fact]
    public void CalculateInSampleSize_Returns2_WhenSourceIsTwiceTarget()
    {
        // 600 / (2*2) = 150 < 300 → loop exits after s=2
        Assert.Equal(2, BitmapUtils.CalculateInSampleSize(600, 300));
    }

    [Fact]
    public void CalculateInSampleSize_Returns4_WhenSourceIsFourTimesTarget()
    {
        Assert.Equal(4, BitmapUtils.CalculateInSampleSize(1200, 300));
    }

    [Fact]
    public void CalculateInSampleSize_Returns8_WhenSourceIsEightTimesTarget()
    {
        Assert.Equal(8, BitmapUtils.CalculateInSampleSize(2400, 300));
    }

    [Fact]
    public void CalculateInSampleSize_Returns16_ForLargeSource()
    {
        Assert.Equal(16, BitmapUtils.CalculateInSampleSize(4800, 300));
    }

    [Fact]
    public void CalculateInSampleSize_RoundsDownToPowerOfTwo()
    {
        // Source is 3× target. 900 / (2*2) = 225 >= 300? No → s stays 2, not 4.
        Assert.Equal(2, BitmapUtils.CalculateInSampleSize(900, 300));
    }

    [Fact]
    public void CalculateInSampleSize_WorksWithTypicalPhoneResolution()
    {
        // 4000px wide source, 400px target → expect s=8 (4000/(8*2)=250 < 400, exit)
        Assert.Equal(8, BitmapUtils.CalculateInSampleSize(4000, 400));
    }

    [Fact]
    public void CalculateInSampleSize_SmallTarget_MaximumDownsampling()
    {
        // source=2048, target=2: exits when 2048/(s*2) < 2, i.e., s > 512 → returns 1024
        Assert.Equal(1024, BitmapUtils.CalculateInSampleSize(2048, 2));
    }
}
