namespace PhotoPresenterAndroid.Services;

internal static class PresentationUtils
{
    // ── Index navigation ─────────────────────────────────────────────────────

    // Circular forward step; returns 0 for empty list.
    internal static int NextIndex(int current, int count) =>
        count == 0 ? 0 : (current + 1) % count;

    // Circular backward step; returns 0 for empty list.
    internal static int PreviousIndex(int current, int count) =>
        count == 0 ? 0 : (current - 1 + count) % count;

    // Clamps an index to [0, count-1]; returns 0 for empty list.
    internal static int ClampIndex(int index, int count) =>
        Math.Clamp(index, 0, Math.Max(0, count - 1));

    // Session-restore: if savedIndex is the last item (or out of range), return 0
    // so the next session starts fresh rather than re-landing on the final item.
    internal static int ResolveRestoredIndex(int savedIndex, int count) =>
        (savedIndex >= 0 && savedIndex < count - 1) ? savedIndex : 0;

    // ── Zoom / scale ──────────────────────────────────────────────────────────

    // Applies a pinch factor to currentScale, clamped to [minScale, maxScale].
    internal static double ClampedScale(double currentScale, double factor,
        double minScale = 1.0, double maxScale = 5.0) =>
        Math.Clamp(currentScale * factor, minScale, maxScale);

    // Whether a completed pinch scale is small enough to auto-snap back to 1×.
    internal static bool ShouldResetZoom(double scale, double threshold = 1.1) =>
        scale < threshold;

    // ── Gesture direction ────────────────────────────────────────────────────

    internal enum SwipeDirection { None, Next, Previous }

    // Android fling: navigates when |velocityX| exceeds threshold px/s.
    // Negative velocity (swipe left) → Next; positive (swipe right) → Previous.
    internal static SwipeDirection GetFlingDirection(float velocityX, float threshold = 300f) =>
        Math.Abs(velocityX) <= threshold ? SwipeDirection.None :
        velocityX < 0 ? SwipeDirection.Next : SwipeDirection.Previous;

    // MAUI pan swipe: navigates when |totalX| exceeds threshold px of displacement.
    internal static SwipeDirection GetPanSwipeDirection(double totalX, double threshold = 60.0) =>
        totalX < -threshold ? SwipeDirection.Next :
        totalX > threshold ? SwipeDirection.Previous :
        SwipeDirection.None;

    // ── Video tap state machine ───────────────────────────────────────────────

    internal enum VideoTapAction { Restart, Pause, Resume }

    // Determines the correct action for a single tap on a video.
    internal static VideoTapAction GetVideoTapAction(bool videoEnded, bool isPlaying) =>
        videoEnded ? VideoTapAction.Restart :
        isPlaying ? VideoTapAction.Pause :
        VideoTapAction.Resume;
}
