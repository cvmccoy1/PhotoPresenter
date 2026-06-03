namespace PhotoPresenter.Models;

public class PhotoFolder
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public List<PhotoItem> Photos { get; set; } = new();
    public bool IsRemoved { get; set; }
}
