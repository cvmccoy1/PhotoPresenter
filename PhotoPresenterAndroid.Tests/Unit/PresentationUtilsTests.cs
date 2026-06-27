using PhotoPresenterAndroid.Services;

namespace PhotoPresenterAndroid.Tests.Unit;

public class PresentationUtilsTests
{
    // ── NextIndex ────────────────────────────────────────────────────────────

    [Fact]
    public void NextIndex_MovesForwardByOne()
    {
        Assert.Equal(3, PresentationUtils.NextIndex(2, 10));
    }

    [Fact]
    public void NextIndex_WrapsFromLastItemToFirst()
    {
        Assert.Equal(0, PresentationUtils.NextIndex(4, 5));
    }

    [Fact]
    public void NextIndex_SingleItem_ReturnsSameIndex()
    {
        Assert.Equal(0, PresentationUtils.NextIndex(0, 1));
    }

    [Fact]
    public void NextIndex_EmptyList_ReturnsZero()
    {
        Assert.Equal(0, PresentationUtils.NextIndex(0, 0));
    }

    // ── PreviousIndex ────────────────────────────────────────────────────────

    [Fact]
    public void PreviousIndex_MovesBackwardByOne()
    {
        Assert.Equal(3, PresentationUtils.PreviousIndex(4, 10));
    }

    [Fact]
    public void PreviousIndex_WrapsFromFirstItemToLast()
    {
        Assert.Equal(4, PresentationUtils.PreviousIndex(0, 5));
    }

    [Fact]
    public void PreviousIndex_SingleItem_ReturnsSameIndex()
    {
        Assert.Equal(0, PresentationUtils.PreviousIndex(0, 1));
    }

    [Fact]
    public void PreviousIndex_EmptyList_ReturnsZero()
    {
        Assert.Equal(0, PresentationUtils.PreviousIndex(0, 0));
    }

    // ── ClampIndex ───────────────────────────────────────────────────────────

    [Fact]
    public void ClampIndex_WithinRange_ReturnsUnchanged()
    {
        Assert.Equal(2, PresentationUtils.ClampIndex(2, 5));
    }

    [Fact]
    public void ClampIndex_Negative_ClampsToZero()
    {
        Assert.Equal(0, PresentationUtils.ClampIndex(-3, 5));
    }

    [Fact]
    public void ClampIndex_AboveMax_ClampsToLastIndex()
    {
        Assert.Equal(4, PresentationUtils.ClampIndex(99, 5));
    }

    [Fact]
    public void ClampIndex_EmptyList_ReturnsZero()
    {
        Assert.Equal(0, PresentationUtils.ClampIndex(5, 0));
    }

    [Fact]
    public void ClampIndex_ExactlyLastIndex_ReturnsUnchanged()
    {
        Assert.Equal(4, PresentationUtils.ClampIndex(4, 5));
    }

    // ── ResolveRestoredIndex ─────────────────────────────────────────────────

    [Fact]
    public void ResolveRestoredIndex_MidListItem_RestoredCorrectly()
    {
        // Index 2 out of 5 is not the last → restore it
        Assert.Equal(2, PresentationUtils.ResolveRestoredIndex(2, 5));
    }

    [Fact]
    public void ResolveRestoredIndex_LastItem_RestartsAtZero()
    {
        // Index 4 is the last in a 5-item list → treat as "finished", restart
        Assert.Equal(0, PresentationUtils.ResolveRestoredIndex(4, 5));
    }

    [Fact]
    public void ResolveRestoredIndex_FileNotFound_ReturnsZero()
    {
        // FindIndex returns -1 when file not found
        Assert.Equal(0, PresentationUtils.ResolveRestoredIndex(-1, 5));
    }

    [Fact]
    public void ResolveRestoredIndex_SingleItemList_RestartsAtZero()
    {
        // Only item (idx 0) is also the last item → restart
        Assert.Equal(0, PresentationUtils.ResolveRestoredIndex(0, 1));
    }

    [Fact]
    public void ResolveRestoredIndex_FirstOfTwo_Restored()
    {
        // Index 0 of 2: 0 < (2-1)=1 → restore
        Assert.Equal(0, PresentationUtils.ResolveRestoredIndex(0, 2));
    }

    [Fact]
    public void ResolveRestoredIndex_LastOfTwo_RestartsAtZero()
    {
        // Index 1 of 2 is the last → restart
        Assert.Equal(0, PresentationUtils.ResolveRestoredIndex(1, 2));
    }

    // ── ClampedScale ─────────────────────────────────────────────────────────

    [Fact]
    public void ClampedScale_NormalFactor_MultipliesCorrectly()
    {
        Assert.Equal(3.0, PresentationUtils.ClampedScale(2.0, 1.5));
    }

    [Fact]
    public void ClampedScale_ResultAboveMax_ClampsToMax()
    {
        Assert.Equal(5.0, PresentationUtils.ClampedScale(3.0, 2.0)); // 6.0 → 5.0
    }

    [Fact]
    public void ClampedScale_ResultBelowMin_ClampsToMin()
    {
        Assert.Equal(1.0, PresentationUtils.ClampedScale(1.5, 0.5)); // 0.75 → 1.0
    }

    [Fact]
    public void ClampedScale_AtMax_ReturnsMax()
    {
        Assert.Equal(5.0, PresentationUtils.ClampedScale(5.0, 1.0));
    }

    // ── ShouldResetZoom ──────────────────────────────────────────────────────

    [Fact]
    public void ShouldResetZoom_BelowThreshold_ReturnsTrue()
    {
        Assert.True(PresentationUtils.ShouldResetZoom(1.05));
    }

    [Fact]
    public void ShouldResetZoom_AboveThreshold_ReturnsFalse()
    {
        Assert.False(PresentationUtils.ShouldResetZoom(1.15));
    }

    [Fact]
    public void ShouldResetZoom_AtThreshold_ReturnsFalse()
    {
        // Boundary: 1.1 is NOT less than 1.1, so no reset
        Assert.False(PresentationUtils.ShouldResetZoom(1.1));
    }

    [Fact]
    public void ShouldResetZoom_AtOne_ReturnsTrue()
    {
        Assert.True(PresentationUtils.ShouldResetZoom(1.0));
    }

    // ── GetFlingDirection ────────────────────────────────────────────────────

    [Fact]
    public void GetFlingDirection_LowVelocity_ReturnsNone()
    {
        Assert.Equal(PresentationUtils.SwipeDirection.None,
            PresentationUtils.GetFlingDirection(200f));
    }

    [Fact]
    public void GetFlingDirection_AtThreshold_ReturnsNone()
    {
        // 300 is NOT greater than 300 → no navigation
        Assert.Equal(PresentationUtils.SwipeDirection.None,
            PresentationUtils.GetFlingDirection(300f));
    }

    [Fact]
    public void GetFlingDirection_NegativeAboveThreshold_ReturnsNext()
    {
        // Swipe left (negative velocity) = advance to next item
        Assert.Equal(PresentationUtils.SwipeDirection.Next,
            PresentationUtils.GetFlingDirection(-400f));
    }

    [Fact]
    public void GetFlingDirection_PositiveAboveThreshold_ReturnsPrevious()
    {
        // Swipe right (positive velocity) = go to previous item
        Assert.Equal(PresentationUtils.SwipeDirection.Previous,
            PresentationUtils.GetFlingDirection(350f));
    }

    // ── GetPanSwipeDirection ─────────────────────────────────────────────────

    [Fact]
    public void GetPanSwipeDirection_SmallDisplacement_ReturnsNone()
    {
        Assert.Equal(PresentationUtils.SwipeDirection.None,
            PresentationUtils.GetPanSwipeDirection(30.0));
    }

    [Fact]
    public void GetPanSwipeDirection_AtThreshold_ReturnsNone()
    {
        // -60 is not less than -60 → no navigation
        Assert.Equal(PresentationUtils.SwipeDirection.None,
            PresentationUtils.GetPanSwipeDirection(-60.0));
    }

    [Fact]
    public void GetPanSwipeDirection_LeftSwipe_ReturnsNext()
    {
        Assert.Equal(PresentationUtils.SwipeDirection.Next,
            PresentationUtils.GetPanSwipeDirection(-80.0));
    }

    [Fact]
    public void GetPanSwipeDirection_RightSwipe_ReturnsPrevious()
    {
        Assert.Equal(PresentationUtils.SwipeDirection.Previous,
            PresentationUtils.GetPanSwipeDirection(80.0));
    }

    // ── GetVideoTapAction ────────────────────────────────────────────────────

    [Fact]
    public void GetVideoTapAction_VideoEnded_ReturnsRestart()
    {
        Assert.Equal(PresentationUtils.VideoTapAction.Restart,
            PresentationUtils.GetVideoTapAction(videoEnded: true, isPlaying: false));
    }

    [Fact]
    public void GetVideoTapAction_EndedAndPlaying_RestartsOverPlaying()
    {
        // Ended state takes priority over isPlaying
        Assert.Equal(PresentationUtils.VideoTapAction.Restart,
            PresentationUtils.GetVideoTapAction(videoEnded: true, isPlaying: true));
    }

    [Fact]
    public void GetVideoTapAction_PlayingNotEnded_ReturnsPause()
    {
        Assert.Equal(PresentationUtils.VideoTapAction.Pause,
            PresentationUtils.GetVideoTapAction(videoEnded: false, isPlaying: true));
    }

    [Fact]
    public void GetVideoTapAction_PausedNotEnded_ReturnsResume()
    {
        Assert.Equal(PresentationUtils.VideoTapAction.Resume,
            PresentationUtils.GetVideoTapAction(videoEnded: false, isPlaying: false));
    }
}
