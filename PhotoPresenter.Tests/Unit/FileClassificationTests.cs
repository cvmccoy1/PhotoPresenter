using PhotoPresenter.Services;

namespace PhotoPresenter.Tests.Unit;

public class FileClassificationTests
{
    // ── IsMediaFile ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.png")]
    [InlineData("photo.bmp")]
    [InlineData("photo.gif")]
    [InlineData("photo.tiff")]
    [InlineData("photo.tif")]
    [InlineData("photo.heic")]
    [InlineData("photo.heif")]
    public void IsMediaFile_PhotoExtension_ReturnsTrue(string path)
    {
        Assert.True(PhotoLibraryService.IsMediaFile(path));
    }

    [Theory]
    [InlineData("video.mov")]
    [InlineData("video.mp4")]
    [InlineData("video.avi")]
    [InlineData("video.wmv")]
    [InlineData("video.m4v")]
    [InlineData("video.mkv")]
    public void IsMediaFile_VideoExtension_ReturnsTrue(string path)
    {
        Assert.True(PhotoLibraryService.IsMediaFile(path));
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.pdf")]
    [InlineData("document.docx")]
    [InlineData("archive.zip")]
    [InlineData("noextension")]
    [InlineData("_photoorder.json")]
    [InlineData("_photofolderorder.json")]
    public void IsMediaFile_NonMediaFile_ReturnsFalse(string path)
    {
        Assert.False(PhotoLibraryService.IsMediaFile(path));
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.JPEG")]
    [InlineData("video.MP4")]
    [InlineData("photo.HEIC")]
    [InlineData("video.MOV")]
    public void IsMediaFile_UpperCaseExtension_ReturnsTrue(string path)
    {
        Assert.True(PhotoLibraryService.IsMediaFile(path));
    }

    // ── IsVideoFile ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.mov")]
    [InlineData("video.avi")]
    [InlineData("video.wmv")]
    [InlineData("video.m4v")]
    [InlineData("video.mkv")]
    public void IsVideoFile_VideoExtension_ReturnsTrue(string path)
    {
        Assert.True(PhotoLibraryService.IsVideoFile(path));
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.heic")]
    [InlineData("document.txt")]
    public void IsVideoFile_NonVideoFile_ReturnsFalse(string path)
    {
        Assert.False(PhotoLibraryService.IsVideoFile(path));
    }

    [Fact]
    public void IsVideoFile_UpperCaseExtension_ReturnsTrue()
    {
        Assert.True(PhotoLibraryService.IsVideoFile("clip.MP4"));
    }

    [Fact]
    public void IsMediaFile_VideoIsAlsoMedia()
    {
        Assert.True(PhotoLibraryService.IsMediaFile("clip.mp4"));
        Assert.True(PhotoLibraryService.IsVideoFile("clip.mp4"));
    }

    [Fact]
    public void IsMediaFile_PhotoIsNotVideo()
    {
        Assert.True(PhotoLibraryService.IsMediaFile("photo.jpg"));
        Assert.False(PhotoLibraryService.IsVideoFile("photo.jpg"));
    }
}
