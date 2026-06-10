namespace PhotoPresenter.Services;

public static class TextUtils
{
    public static string NormalizeCaption(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
}
