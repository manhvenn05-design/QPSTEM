using Microsoft.AspNetCore.Http;

namespace STEM.Web.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string folderName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fileUrl);
    /// <summary>
    /// Trả về URL có thể sử dụng trực tiếp để tải file (nếu cần xác thực riêng).
    /// Với Local Storage, trả về URL gốc không cần chỉnh sửa.
    /// </summary>
    string GetAuthenticatedDownloadUrl(string fileUrl);
}
