using PhotoPresenter.Models;
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

    // ── GetEffectiveDateWithExif ──────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveDateWithExif_VideoItem_SkipsExifUsesFileDate()
    {
        var path = _tmp.CreateFile(@"clip.mp4");
        var item = new PhotoItem { FileName = "clip.mp4", FullPath = path, IsVideo = true };

        var result = _service.GetEffectiveDateWithExif(item);

        // File exists so result must be the file's LastWriteTime, not default.
        Assert.NotEqual(default, result);
    }

    [Fact]
    public void GetEffectiveDateWithExif_NonExistentFile_FallsBackToCreationDate()
    {
        var sentinel = new DateTime(2023, 7, 4, 12, 0, 0);
        var item = new PhotoItem
        {
            FileName     = "ghost.jpg",
            FullPath     = Path.Combine(_tmp.Path, "ghost.jpg"),
            IsVideo      = false,
            CreationDate = sentinel
        };

        var result = _service.GetEffectiveDateWithExif(item);

        // File does not exist — TryGetExifDate catches the FileNotFoundException and returns
        // null; the outer method then falls to file.Exists check which is false, returning
        // item.CreationDate.
        Assert.Equal(sentinel, result);
    }

    [Fact]
    public void GetEffectiveDateWithExif_JpegWithNoExif_FallsBackToFileDate()
    {
        // TinyJpegBytes is a minimal 1×1 JPEG with no EXIF block — DateTaken is absent.
        var path = _tmp.CreateFile("photo.jpg", TempDirectory.TinyJpegBytes);
        var item = new PhotoItem { FileName = "photo.jpg", FullPath = path, IsVideo = false };

        var result = _service.GetEffectiveDateWithExif(item);

        // TryGetExifDate: opens file, finds no DateTaken, returns null.
        // Outer method: file.Exists=true → returns file.LastWriteTime.
        Assert.NotEqual(default, result);
    }

    [Fact]
    public void GetEffectiveDateWithExif_VideoNonExistentFile_FallsBackToCreationDate()
    {
        var sentinel = new DateTime(2021, 1, 1);
        var item = new PhotoItem
        {
            FileName     = "ghost.mp4",
            FullPath     = Path.Combine(_tmp.Path, "ghost.mp4"),
            IsVideo      = true,
            CreationDate = sentinel
        };

        var result = _service.GetEffectiveDateWithExif(item);

        Assert.Equal(sentinel, result);
    }

    // ── ApplyFolderOrder / LoadPhotosForFolder — null sidecar.Order ───────────────

    [Fact]
    public void ApplyFolderOrder_WhenSidecarOrderIsNull_ReturnsFoldersAlphabetically()
    {
        // sidecar.Order == null → early return at line 78; should fall back to alphabetical
        File.WriteAllText(Path.Combine(_tmp.Path, "_photofolderorder.json"), """{"order":null}""");
        Directory.CreateDirectory(Path.Combine(_tmp.Path, "Bravo"));
        Directory.CreateDirectory(Path.Combine(_tmp.Path, "Alpha"));
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, removed) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(2, active.Count);
        Assert.Equal("Alpha", active[0].Name);
        Assert.Equal("Bravo", active[1].Name);
        Assert.Empty(removed);
    }

    [Fact]
    public void LoadPhotosForFolder_WhenSidecarOrderIsNull_ReturnsPhotosByDate()
    {
        // sidecar.Order == null → early return at lines 122-124; falls back to date order
        File.WriteAllText(Path.Combine(_tmp.Path, "_photoorder.json"), """{"order":null}""");
        File.WriteAllBytes(Path.Combine(_tmp.Path, "img.jpg"), TempDirectory.TinyJpegBytes);

        var result = PhotoLibraryService.LoadPhotosForFolder(_tmp.Path);

        Assert.Single(result);
        Assert.Equal("img.jpg", result[0].FileName);
    }

}
