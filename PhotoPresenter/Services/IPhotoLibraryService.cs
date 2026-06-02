using PhotoPresenter.Models;

namespace PhotoPresenter.Services;

public interface IPhotoLibraryService
{
    Task<List<PhotoFolder>> LoadLibraryAsync(string parentPath);
    void SaveFolderOrder(string parentPath, IEnumerable<PhotoFolder> folders);
    void SavePhotoOrder(PhotoFolder folder, IEnumerable<PhotoItem> photos);
}
