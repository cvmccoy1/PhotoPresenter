namespace PhotoPresenterAndroid.Services;

internal static class BitmapUtils
{
    // Returns the largest power-of-two inSampleSize that keeps decoded width >= targetWidth.
    internal static int CalculateInSampleSize(int sourceWidth, int targetWidth)
    {
        int s = 1;
        while (sourceWidth / (s * 2) >= targetWidth)
            s *= 2;
        return s;
    }
}
