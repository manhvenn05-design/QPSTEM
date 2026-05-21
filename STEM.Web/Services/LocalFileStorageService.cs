using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace STEM.Web.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    // Các loại file được phép tải lên
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
        ".mp4", ".webm", ".mov",
        ".pdf"
    };

    // Video được phép tối đa 100 MB, file khác tối đa 25 MB
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov"
    };

    private const long MaxVideoSizeBytes = 100L * 1024 * 1024; // 100 MB
    private const long MaxFileSizeBytes  =  25L * 1024 * 1024; //  25 MB

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string folderName, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Vui lòng chọn tệp hợp lệ.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Chỉ hỗ trợ tệp JPG, JPEG, PNG, WEBP, MP4, WEBM, MOV hoặc PDF.");

        var isVideo = VideoExtensions.Contains(extension);
        var maxSize = isVideo ? MaxVideoSizeBytes : MaxFileSizeBytes;
        var maxLabel = isVideo ? "100MB" : "25MB";

        if (file.Length > maxSize)
            throw new InvalidOperationException($"Tệp tải lên không được vượt quá {maxLabel}.");

        // Đảm bảo thư mục tồn tại
        var uploadPath = Path.Combine(_env.WebRootPath, "uploads", folderName);
        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);

        // Tên file an toàn, thêm GUID để tránh trùng lặp
        var safeOriginal = Path.GetFileNameWithoutExtension(file.FileName);
        var safeName     = new string(safeOriginal.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        var fileName     = $"{Guid.NewGuid():N}_{safeName}{extension}";
        var filePath     = Path.Combine(uploadPath, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                         bufferSize: 81920, useAsync: true))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        // Trả về URL tương đối, dùng cho src/href trong HTML
        return $"/uploads/{folderName}/{fileName}";
    }

    public Task DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || !fileUrl.StartsWith("/uploads/"))
            return Task.CompletedTask;

        try
        {
            // Chuyển URL tương đối → đường dẫn vật lý
            var relativePath = fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath     = Path.Combine(_env.WebRootPath, relativePath);

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Bỏ qua lỗi xóa để không block luồng chính
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Với Local Storage, file phục vụ trực tiếp qua static files middleware →
    /// không cần xác thực bổ sung, trả về nguyên URL.
    /// </summary>
    public string GetAuthenticatedDownloadUrl(string fileUrl) => fileUrl;
}
