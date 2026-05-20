using Microsoft.AspNetCore.Http;

namespace STEM.Web.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string folderName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fileUrl);
}
