namespace PhotoPresenter.Models;

public class PhotoItem
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime CreationDate { get; set; }
    public bool IsRemoved { get; set; }
    public bool IsVideo { get; set; }
    public bool IsMirrored { get; set; }
    public bool IsFavorite { get; set; }
    public string Caption { get; set; } = "";
    public int Brightness { get; set; }
    public int Contrast { get; set; }
}
