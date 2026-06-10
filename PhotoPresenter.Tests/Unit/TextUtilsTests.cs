using PhotoPresenter.Services;

namespace PhotoPresenter.Tests.Unit;

public class TextUtilsTests
{
    [Theory]
    [InlineData("hello\r\nworld", "hello\nworld")]
    [InlineData("hello\rworld",   "hello\nworld")]
    [InlineData("hello\nworld",   "hello\nworld")]
    public void NormalizeCaption_LineEndings_ConvertedToLf(string input, string expected)
    {
        Assert.Equal(expected, TextUtils.NormalizeCaption(input));
    }

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("\nhello\n", "hello")]
    [InlineData("  \n  hello  \n  ", "hello")]
    public void NormalizeCaption_LeadingTrailingWhitespace_Trimmed(string input, string expected)
    {
        Assert.Equal(expected, TextUtils.NormalizeCaption(input));
    }

    [Fact]
    public void NormalizeCaption_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", TextUtils.NormalizeCaption(""));
    }

    [Fact]
    public void NormalizeCaption_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal("", TextUtils.NormalizeCaption("   \r\n\t  "));
    }

    [Fact]
    public void NormalizeCaption_MixedLineEndings_AllNormalized()
    {
        var input = "a\r\nb\rc\nd";
        Assert.Equal("a\nb\nc\nd", TextUtils.NormalizeCaption(input));
    }
}
