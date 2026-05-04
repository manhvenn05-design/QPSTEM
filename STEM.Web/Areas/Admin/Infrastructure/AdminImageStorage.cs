using Microsoft.AspNetCore.Http;

namespace STEM.Web.Areas.Admin.Infrastructure;

public static class AdminImageStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static async Task<string> SaveImageAsync(IFormFile file, string webRootPath, string folderName, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Vui lòng chọn tệp ảnh hợp lệ.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Ảnh tải lên không được vượt quá 5MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Chỉ hỗ trợ ảnh JPG, JPEG, PNG hoặc WEBP.");
        }

        var relativeFolder = Path.Combine("uploads", folderName);
        var absoluteFolder = Path.Combine(webRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var absolutePath = Path.Combine(absoluteFolder, fileName);

        await using var stream = new FileStream(absolutePath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        return "/" + Path.Combine(relativeFolder, fileName).Replace("\\", "/");
    }

    public static void DeleteIfManaged(string? relativePath, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var trimmed = relativePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
        var absolutePath = Path.Combine(webRootPath, trimmed);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
}
