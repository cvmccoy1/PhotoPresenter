using PhotoPresenterAndroid.Models;

namespace PhotoPresenterAndroid.Tests.Unit;

public class MediaItemTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var item = new MediaItem("/path/to/img.jpg", "A caption", false);
        Assert.Equal("/path/to/img.jpg", item.FullPath);
        Assert.Equal("A caption", item.Caption);
        Assert.False(item.IsVideo);
    }

    [Fact]
    public void Constructor_NullCaption_IsAllowed()
    {
        var item = new MediaItem("/path/video.mp4", null, true);
        Assert.Null(item.Caption);
        Assert.True(item.IsVideo);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new MediaItem("/img.jpg", "cap", false);
        var b = new MediaItem("/img.jpg", "cap", false);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentPath_AreNotEqual()
    {
        var a = new MediaItem("/a.jpg", null, false);
        var b = new MediaItem("/b.jpg", null, false);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentCaption_AreNotEqual()
    {
        var a = new MediaItem("/img.jpg", "hello", false);
        var b = new MediaItem("/img.jpg", null,    false);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WithExpression_OverridesCaption()
    {
        var original = new MediaItem("/img.jpg", null, false);
        var copy = original with { Caption = "New caption" };
        Assert.Equal("/img.jpg", copy.FullPath);
        Assert.Equal("New caption", copy.Caption);
        Assert.False(copy.IsVideo);
    }
}
