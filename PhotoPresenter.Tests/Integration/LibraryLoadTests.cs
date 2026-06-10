using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;

namespace PhotoPresenter.Tests.Integration;

public class LibraryLoadTests : IDisposable
{
    private readonly TempDirectory _tmp = new();
    private readonly PhotoLibraryService _service = new();

    public void Dispose() => _tmp.Dispose();

    [Fact]
    public async Task LoadLibraryAsync_NonExistentPath_ReturnsEmptyList()
    {
        var result = await _service.LoadLibraryAsync(@"Z:\definitely\does\not\exist");

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadLibraryAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var result = await _service.LoadLibraryAsync(_tmp.Path);

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadLibraryAsync_SubdirsWithPhotos_ReturnsAllFolders()
    {
        _tmp.CreateSubDir("FolderA");
        _tmp.CreateSubDir("FolderB");
        _tmp.CreateFile(@"FolderA\photo.jpg");
        _tmp.CreateFile(@"FolderB\photo.jpg");

        var result = await _service.LoadLibraryAsync(_tmp.Path);

        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Single(f.Photos));
    }

    [Fact]
    public async Task LoadLibraryAsync_NonMediaFilesIgnored()
    {
        _tmp.CreateSubDir("FolderA");
        _tmp.CreateFile(@"FolderA\photo.jpg");
        File.WriteAllText(System.IO.Path.Combine(_tmp.Path, "FolderA", "readme.txt"), "text");

        var result = await _service.LoadLibraryAsync(_tmp.Path);

        var photos = result.Single().Photos;
        Assert.Single(photos);
        Assert.Equal("photo.jpg", photos[0].FileName);
    }

    [Fact]
    public async Task LoadLibraryAsync_NoSidecar_FoldersReturnedAlphabetically()
    {
        _tmp.CreateSubDir("Zulu");
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Mike");

        var result = await _service.LoadLibraryAsync(_tmp.Path);

        var names = result.Where(f => !f.IsRemoved).Select(f => f.Name).ToList();
        Assert.Equal(new[] { "Alpha", "Mike", "Zulu" }, names);
    }
}
