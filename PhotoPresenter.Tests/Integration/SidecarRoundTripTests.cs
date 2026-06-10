using System.Text.Json;
using PhotoPresenter.Models;
using PhotoPresenter.Services;
using PhotoPresenter.Tests.Infrastructure;

namespace PhotoPresenter.Tests.Integration;

public class SidecarRoundTripTests : IDisposable
{
    private readonly TempDirectory _tmp = new();
    private readonly PhotoLibraryService _service = new();

    public void Dispose() => _tmp.Dispose();

    // ── Photo order ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavePhotoOrder_ThenLoad_PreservesPhotoOrder()
    {
        var dir = _tmp.CreateSubDir("FolderA");
        _tmp.CreateFile(@"FolderA\a.jpg");
        _tmp.CreateFile(@"FolderA\b.jpg");
        _tmp.CreateFile(@"FolderA\c.jpg");
        var folder = MakeFolder(dir, "a.jpg", "b.jpg", "c.jpg");

        // Save in reverse order
        var reordered = folder.Photos.OrderByDescending(p => p.FileName).ToList();
        folder.Photos.Clear();
        reordered.ForEach(p => folder.Photos.Add(p));
        _service.SavePhotoOrder(folder, folder.Photos);

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);
        var names = loaded.Single(f => f.Name == "FolderA")
            .Photos.Where(p => !p.IsRemoved).Select(p => p.FileName).ToList();

        Assert.Equal(new[] { "c.jpg", "b.jpg", "a.jpg" }, names);
    }

    [Fact]
    public async Task SavePhotoOrder_ThenLoad_PreservesRemovedPhoto()
    {
        var dir = _tmp.CreateSubDir("FolderB");
        _tmp.CreateFile(@"FolderB\a.jpg");
        _tmp.CreateFile(@"FolderB\b.jpg");
        var folder = MakeFolder(dir, "a.jpg", "b.jpg");
        folder.Photos[1].IsRemoved = true;
        _service.SavePhotoOrder(folder, folder.Photos);

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);
        var photos = loaded.Single(f => f.Name == "FolderB").Photos;

        Assert.True(photos.First(p => p.FileName == "b.jpg").IsRemoved);
        Assert.False(photos.First(p => p.FileName == "a.jpg").IsRemoved);
    }

    [Fact]
    public async Task SavePhotoOrder_ThenLoad_PreservesCaption()
    {
        var dir = _tmp.CreateSubDir("FolderC");
        _tmp.CreateFile(@"FolderC\a.jpg");
        var folder = MakeFolder(dir, "a.jpg");
        folder.Photos[0].Caption = "Hello\nWorld";
        _service.SavePhotoOrder(folder, folder.Photos);

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);
        var photo = loaded.Single(f => f.Name == "FolderC").Photos.Single();

        Assert.Equal("Hello\nWorld", photo.Caption);
    }

    [Fact]
    public async Task SavePhotoOrder_ThenLoad_PreservesMirroredFlag()
    {
        var dir = _tmp.CreateSubDir("FolderD");
        _tmp.CreateFile(@"FolderD\a.jpg");
        var folder = MakeFolder(dir, "a.jpg");
        folder.Photos[0].IsMirrored = true;
        _service.SavePhotoOrder(folder, folder.Photos);

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);
        var photo = loaded.Single(f => f.Name == "FolderD").Photos.Single();

        Assert.True(photo.IsMirrored);
    }

    [Fact]
    public void SavePhotoOrder_NoCaptions_CaptionsKeyAbsentInJson()
    {
        var dir = _tmp.CreateSubDir("FolderE");
        _tmp.CreateFile(@"FolderE\a.jpg");
        var folder = MakeFolder(dir, "a.jpg"); // no caption set
        _service.SavePhotoOrder(folder, folder.Photos);

        var json = File.ReadAllText(System.IO.Path.Combine(dir, "_photoorder.json"));
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("captions", out _));
    }

    [Fact]
    public void SavePhotoOrder_NoMirrored_MirroredKeyAbsentInJson()
    {
        var dir = _tmp.CreateSubDir("FolderF");
        _tmp.CreateFile(@"FolderF\a.jpg");
        var folder = MakeFolder(dir, "a.jpg"); // not mirrored
        _service.SavePhotoOrder(folder, folder.Photos);

        var json = File.ReadAllText(System.IO.Path.Combine(dir, "_photoorder.json"));
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("mirrored", out _));
    }

    [Fact]
    public async Task SavePhotoOrder_NewFileAddedExternally_AppearsOnReload()
    {
        var dir = _tmp.CreateSubDir("FolderG");
        _tmp.CreateFile(@"FolderG\a.jpg");
        _tmp.CreateFile(@"FolderG\b.jpg");
        var folder = MakeFolder(dir, "a.jpg", "b.jpg");
        _service.SavePhotoOrder(folder, folder.Photos);

        // External addition after saving
        _tmp.CreateFile(@"FolderG\c.jpg");

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);
        var names = loaded.Single(f => f.Name == "FolderG")
            .Photos.Where(p => !p.IsRemoved).Select(p => p.FileName).ToList();

        Assert.Contains("c.jpg", names);
        // c.jpg appended after the sidecar-ordered a and b
        Assert.Equal("a.jpg", names[0]);
        Assert.Equal("b.jpg", names[1]);
    }

    // ── Folder order ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveFolderOrder_ThenLoad_FolderOrderPreserved()
    {
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Bravo");
        _tmp.CreateSubDir("Charlie");
        var folders = new List<PhotoFolder>
        {
            new() { Name = "Charlie", FullPath = System.IO.Path.Combine(_tmp.Path, "Charlie") },
            new() { Name = "Alpha",   FullPath = System.IO.Path.Combine(_tmp.Path, "Alpha") },
            new() { Name = "Bravo",   FullPath = System.IO.Path.Combine(_tmp.Path, "Bravo") },
        };
        _service.SaveFolderOrder(_tmp.Path, folders);

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);
        var names = loaded.Where(f => !f.IsRemoved).Select(f => f.Name).ToList();

        Assert.Equal(new[] { "Charlie", "Alpha", "Bravo" }, names);
    }

    [Fact]
    public async Task SaveFolderOrder_ThenLoad_RemovedFolderPreserved()
    {
        _tmp.CreateSubDir("Alpha");
        _tmp.CreateSubDir("Bravo");
        var folders = new List<PhotoFolder>
        {
            new() { Name = "Alpha", FullPath = System.IO.Path.Combine(_tmp.Path, "Alpha") },
            new() { Name = "Bravo", FullPath = System.IO.Path.Combine(_tmp.Path, "Bravo"), IsRemoved = true },
        };
        _service.SaveFolderOrder(_tmp.Path, folders);

        var loaded = await _service.LoadLibraryAsync(_tmp.Path);

        Assert.True(loaded.First(f => f.Name == "Bravo").IsRemoved);
        Assert.False(loaded.First(f => f.Name == "Alpha").IsRemoved);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static PhotoFolder MakeFolder(string fullPath, params string[] fileNames)
    {
        var name = System.IO.Path.GetFileName(fullPath);
        return new PhotoFolder
        {
            Name = name,
            FullPath = fullPath,
            Photos = fileNames.Select(f => new PhotoItem
            {
                FileName = f,
                FullPath = System.IO.Path.Combine(fullPath, f)
            }).ToList()
        };
    }
}
