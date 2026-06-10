using System.Text.Json;
using PhotoPresenter.Models;
using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;

namespace PhotoPresenter.Tests.Unit;

public class SidecarParsingTests : IDisposable
{
    private readonly TempDirectory _tmp = new();

    public void Dispose() => _tmp.Dispose();

    // ── ApplyFolderOrder ─────────────────────────────────────────────────────────

    [Fact]
    public void ApplyFolderOrder_NoSidecar_ReturnsDirsAlphabetically()
    {
        _tmp.CreateSubDir("Zulu");
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Mike");
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, removed) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(new[] { "Alpha", "Mike", "Zulu" }, active.Select(d => d.Name));
        Assert.Empty(removed);
    }

    [Fact]
    public void ApplyFolderOrder_WithSidecar_ReturnsInSidecarOrder()
    {
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Bravo");
        _tmp.CreateSubDir("Charlie");
        WriteFolderSidecar(_tmp.Path, order: ["Charlie", "Alpha", "Bravo"]);
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, _) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(new[] { "Charlie", "Alpha", "Bravo" }, active.Select(d => d.Name));
    }

    [Fact]
    public void ApplyFolderOrder_NewDirNotInSidecar_AppendedAlphabetically()
    {
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Bravo");
        _tmp.CreateSubDir("Zulu");   // new — not in sidecar
        WriteFolderSidecar(_tmp.Path, order: ["Bravo", "Alpha"]);
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, _) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(new[] { "Bravo", "Alpha", "Zulu" }, active.Select(d => d.Name));
    }

    [Fact]
    public void ApplyFolderOrder_RemovedDirExistsOnDisk_ReturnedInRemovedList()
    {
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Bravo");
        WriteFolderSidecar(_tmp.Path, order: ["Alpha"], removed: ["Bravo"]);
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, removed) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(new[] { "Alpha" }, active.Select(d => d.Name));
        Assert.Equal(new[] { "Bravo" }, removed);
    }

    [Fact]
    public void ApplyFolderOrder_SidecarEntryForMissingDir_Skipped()
    {
        _tmp.CreateSubDir("Alpha");
        WriteFolderSidecar(_tmp.Path, order: ["Alpha", "Ghost"]);  // Ghost doesn't exist on disk
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, _) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(new[] { "Alpha" }, active.Select(d => d.Name));
    }

    [Fact]
    public void ApplyFolderOrder_CorruptSidecar_FallsBackToAlphabetical()
    {
        _tmp.CreateSubDir("Zulu");
        _tmp.CreateSubDir("Alpha");
        File.WriteAllText(System.IO.Path.Combine(_tmp.Path, "_photofolderorder.json"), "{ NOT VALID JSON ]]]");
        var subdirs = new DirectoryInfo(_tmp.Path).GetDirectories().ToList();

        var (active, removed) = PhotoLibraryService.ApplyFolderOrder(_tmp.Path, subdirs);

        Assert.Equal(new[] { "Alpha", "Zulu" }, active.Select(d => d.Name));
        Assert.Empty(removed);
    }

    // ── LoadPhotosForFolder ──────────────────────────────────────────────────────

    [Fact]
    public void LoadPhotosForFolder_NoSidecar_ReturnsFilesOrderedByDate()
    {
        var dir = _tmp.CreateSubDir("FolderA");
        var p1 = System.IO.Path.Combine(dir, "a.jpg");
        var p2 = System.IO.Path.Combine(dir, "b.jpg");
        var p3 = System.IO.Path.Combine(dir, "c.jpg");
        File.WriteAllBytes(p1, TempDirectory.TinyJpegBytes);
        File.WriteAllBytes(p2, TempDirectory.TinyJpegBytes);
        File.WriteAllBytes(p3, TempDirectory.TinyJpegBytes);
        // Set explicit dates so ordering is deterministic
        File.SetLastWriteTime(p1, new DateTime(2024, 1, 3));
        File.SetCreationTime(p1, new DateTime(2024, 1, 3));
        File.SetLastWriteTime(p2, new DateTime(2024, 1, 1));
        File.SetCreationTime(p2, new DateTime(2024, 1, 1));
        File.SetLastWriteTime(p3, new DateTime(2024, 1, 2));
        File.SetCreationTime(p3, new DateTime(2024, 1, 2));

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.Equal(new[] { "b.jpg", "c.jpg", "a.jpg" },
            photos.Where(p => !p.IsRemoved).Select(p => p.FileName));
    }

    [Fact]
    public void LoadPhotosForFolder_WithSidecar_ActiveFilesInSidecarOrder()
    {
        var dir = _tmp.CreateSubDir("FolderB");
        _tmp.CreateFile(@"FolderB\a.jpg");
        _tmp.CreateFile(@"FolderB\b.jpg");
        _tmp.CreateFile(@"FolderB\c.jpg");
        WritePhotoSidecar(dir, order: ["c.jpg", "a.jpg", "b.jpg"]);

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.Equal(new[] { "c.jpg", "a.jpg", "b.jpg" },
            photos.Where(p => !p.IsRemoved).Select(p => p.FileName));
    }

    [Fact]
    public void LoadPhotosForFolder_NewFileNotInSidecar_AppendedAfterOrdered()
    {
        var dir = _tmp.CreateSubDir("FolderC");
        _tmp.CreateFile(@"FolderC\a.jpg");
        _tmp.CreateFile(@"FolderC\b.jpg");
        _tmp.CreateFile(@"FolderC\new.jpg");  // not in sidecar
        WritePhotoSidecar(dir, order: ["b.jpg", "a.jpg"]);

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);
        var activeNames = photos.Where(p => !p.IsRemoved).Select(p => p.FileName).ToList();

        Assert.Equal("b.jpg", activeNames[0]);
        Assert.Equal("a.jpg", activeNames[1]);
        Assert.Contains("new.jpg", activeNames);
    }

    [Fact]
    public void LoadPhotosForFolder_RemovedFileStillOnDisk_ReturnedAsRemoved()
    {
        var dir = _tmp.CreateSubDir("FolderD");
        _tmp.CreateFile(@"FolderD\a.jpg");
        _tmp.CreateFile(@"FolderD\removed.jpg");
        WritePhotoSidecar(dir, order: ["a.jpg"], removed: ["removed.jpg"]);

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        var removed = photos.Where(p => p.IsRemoved).Select(p => p.FileName).ToList();
        Assert.Contains("removed.jpg", removed);
    }

    [Fact]
    public void LoadPhotosForFolder_SidecarEntryForMissingFile_Skipped()
    {
        var dir = _tmp.CreateSubDir("FolderE");
        _tmp.CreateFile(@"FolderE\a.jpg");
        WritePhotoSidecar(dir, order: ["a.jpg", "ghost.jpg"]);

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.DoesNotContain(photos, p => p.FileName == "ghost.jpg");
    }

    [Fact]
    public void LoadPhotosForFolder_CaptionApplied()
    {
        var dir = _tmp.CreateSubDir("FolderF");
        _tmp.CreateFile(@"FolderF\a.jpg");
        WritePhotoSidecar(dir, order: ["a.jpg"],
            captions: new Dictionary<string, string> { ["a.jpg"] = "Hello\nWorld" });

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.Equal("Hello\nWorld", photos.First().Caption);
    }

    [Fact]
    public void LoadPhotosForFolder_MirroredFlagApplied()
    {
        var dir = _tmp.CreateSubDir("FolderG");
        _tmp.CreateFile(@"FolderG\a.jpg");
        _tmp.CreateFile(@"FolderG\b.jpg");
        WritePhotoSidecar(dir, order: ["a.jpg", "b.jpg"], mirrored: ["a.jpg"]);

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.True(photos.First(p => p.FileName == "a.jpg").IsMirrored);
        Assert.False(photos.First(p => p.FileName == "b.jpg").IsMirrored);
    }

    [Fact]
    public void LoadPhotosForFolder_NonMediaFilesIgnored()
    {
        var dir = _tmp.CreateSubDir("FolderH");
        _tmp.CreateFile(@"FolderH\photo.jpg");
        File.WriteAllText(System.IO.Path.Combine(dir, "readme.txt"), "text");
        File.WriteAllText(System.IO.Path.Combine(dir, "_photoorder.json"), "{}");

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.All(photos, p => Assert.True(PhotoLibraryService.IsMediaFile(p.FileName)));
    }

    [Fact]
    public void LoadPhotosForFolder_CorruptSidecar_FallsBackToDateOrder()
    {
        var dir = _tmp.CreateSubDir("FolderI");
        _tmp.CreateFile(@"FolderI\a.jpg");
        File.WriteAllText(System.IO.Path.Combine(dir, "_photoorder.json"), "{ NOT VALID JSON ]]]");

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.Single(photos);
        Assert.Equal("a.jpg", photos[0].FileName);
    }

    [Fact]
    public void LoadPhotosForFolder_VideoFileRecognized()
    {
        var dir = _tmp.CreateSubDir("FolderJ");
        _tmp.CreateFile(@"FolderJ\clip.mp4");

        var photos = PhotoLibraryService.LoadPhotosForFolder(dir);

        Assert.Single(photos);
        Assert.True(photos[0].IsVideo);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void WriteFolderSidecar(string parentPath,
        IEnumerable<string>? order = null, IEnumerable<string>? removed = null)
    {
        var sidecar = new
        {
            order   = order?.ToList() ?? new List<string>(),
            removed = removed?.ToList() ?? new List<string>()
        };
        File.WriteAllText(
            System.IO.Path.Combine(parentPath, "_photofolderorder.json"),
            JsonSerializer.Serialize(sidecar));
    }

    private static void WritePhotoSidecar(string folderPath,
        IEnumerable<string>? order = null,
        IEnumerable<string>? removed = null,
        Dictionary<string, string>? captions = null,
        IEnumerable<string>? mirrored = null)
    {
        var sidecar = new
        {
            order    = order?.ToList() ?? new List<string>(),
            removed  = removed?.ToList() ?? new List<string>(),
            captions,
            mirrored = mirrored?.ToList()
        };
        File.WriteAllText(
            System.IO.Path.Combine(folderPath, "_photoorder.json"),
            JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
    }
}
