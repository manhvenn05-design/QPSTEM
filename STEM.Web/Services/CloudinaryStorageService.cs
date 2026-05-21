using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace STEM.Web.Services;

public class CloudinaryStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".mp4",
        ".pdf"
    };
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25MB cho cả video và ảnh

    public CloudinaryStorageService(IConfiguration configuration)
    {
        var account = new Account(
            configuration["Cloudinary:CloudName"],
            configuration["Cloudinary:ApiKey"],
            configuration["Cloudinary:ApiSecret"]
        );
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string folderName, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Vui lòng chọn tệp hợp lệ.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Tệp tải lên không được vượt quá 25MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Chỉ hỗ trợ tệp JPG, JPEG, PNG, WEBP, MP4 hoặc PDF.");
        }

        var isVideo = extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        var isPdf = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        await using var stream = file.OpenReadStream();
        
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
        // Thay thế các ký tự không hợp lệ cho PublicId
        var safeFileName = new string(fileNameWithoutExtension.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        var publicId = $"{Guid.NewGuid():N}_{safeFileName}";

        if (isVideo)
        {
            var uploadParams = new VideoUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                PublicId = publicId,
                Folder = $"qpstem/{folderName}"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            if (uploadResult.Error != null) throw new InvalidOperationException($"Lỗi tải lên: {uploadResult.Error.Message}");
            return uploadResult.SecureUrl.ToString();
        }
        else if (isPdf)
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                PublicId = publicId,
                Folder = $"qpstem/{folderName}"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            if (uploadResult.Error != null) throw new InvalidOperationException($"Lỗi tải lên: {uploadResult.Error.Message}");
            
            // Cloudinary mặc định chặn tải/truy cập PDF (báo lỗi 401) đối với các tài khoản miễn phí.
            // Để bypass, chúng ta bắt buộc phải sử dụng Signed URL cho tệp tin này.
            var signedUrl = _cloudinary.Api.UrlImgUp
                .Secure(true)
                .Format("pdf")
                .Signed(true)
                .BuildUrl(uploadResult.PublicId);

            return signedUrl;
        }
        else
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(file.FileName, stream),
                PublicId = publicId,
                Folder = $"qpstem/{folderName}"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            if (uploadResult.Error != null) throw new InvalidOperationException($"Lỗi tải lên: {uploadResult.Error.Message}");
            return uploadResult.SecureUrl.ToString();
        }
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || !fileUrl.Contains("cloudinary.com"))
        {
            return;
        }

        try
        {
            // Trích xuất publicId từ URL
            // Format phổ biến: https://res.cloudinary.com/cloudname/image/upload/v1234567890/folder/filename.ext
            var uri = new Uri(fileUrl);
            var pathSegments = uri.AbsolutePath.Split('/');
            
            // Tìm index của 'upload'
            var uploadIndex = Array.IndexOf(pathSegments, "upload");
            if (uploadIndex >= 0 && uploadIndex + 2 < pathSegments.Length)
            {
                // Bỏ qua version (v12345...)
                var publicIdSegments = pathSegments.Skip(uploadIndex + 2);
                var publicIdWithExt = string.Join("/", publicIdSegments);
                
                // Bỏ đuôi mở rộng
                var lastDotIndex = publicIdWithExt.LastIndexOf('.');
                var publicId = lastDotIndex > 0 ? publicIdWithExt[..lastDotIndex] : publicIdWithExt;

                // Cố gắng đoán type (image hay video) từ url
                var type = uri.AbsolutePath.Contains("/video/") ? ResourceType.Video : ResourceType.Image;
                
                var deletionParams = new DeletionParams(publicId)
                {
                    ResourceType = type
                };
                
                await _cloudinary.DestroyAsync(deletionParams);
            }
        }
        catch
        {
            // Bỏ qua lỗi xóa nếu có để không block flow chính
        }
    }

    /// <summary>
    /// Sinh ra URL tải có xác thực qua Cloudinary Admin API Download endpoint.
    /// URL này bypass hoàn toàn mọi policy chặn PDF delivery công khai của Cloudinary.
    /// </summary>
    public string GetAuthenticatedDownloadUrl(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || !fileUrl.Contains("cloudinary.com"))
            return fileUrl;

        try
        {
            var uri = new Uri(fileUrl);
            var pathSegments = uri.AbsolutePath.Split('/');

            // Tìm vị trí "upload" để lấy phần publicId
            var uploadIndex = Array.IndexOf(pathSegments, "upload");
            if (uploadIndex < 0) return fileUrl;

            // Bỏ qua phần version (vXXX) hoặc signature (s--xxx--)
            var afterUpload = pathSegments.Skip(uploadIndex + 1).ToArray();
            int startIdx = 0;
            if (afterUpload.Length > 0 &&
                ((afterUpload[0].StartsWith("v") && afterUpload[0].Length > 1 && char.IsDigit(afterUpload[0][1]))
                 || afterUpload[0].StartsWith("s--")))
            {
                startIdx = 1;
            }

            var publicIdWithExt = string.Join("/", afterUpload.Skip(startIdx));

            // Xác định resource type từ URL path
            var resourceType = uri.AbsolutePath.Contains("/video/") ? "video"
                : uri.AbsolutePath.Contains("/raw/") ? "raw"
                : "image";

            // Tách extension và publicId
            var lastDot = publicIdWithExt.LastIndexOf('.');
            var format = lastDot > 0 ? publicIdWithExt[(lastDot + 1)..] : "";
            var publicId = lastDot > 0 ? publicIdWithExt[..lastDot] : publicIdWithExt;

            // Lấy credentials từ SDK
            var cloudName = _cloudinary.Api.Account.Cloud;
            var apiKey    = _cloudinary.Api.Account.ApiKey;
            var apiSecret = _cloudinary.Api.Account.ApiSecret;

            // Tính timestamp và signature theo chuẩn Cloudinary
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var toSign    = $"public_id={publicId}&timestamp={timestamp}{apiSecret}";
            var signature = ComputeSha1(toSign);

            // Tạo Admin API download URL
            var downloadUrl = $"https://api.cloudinary.com/v1_1/{cloudName}/{resourceType}/download" +
                              $"?public_id={Uri.EscapeDataString(publicId)}" +
                              $"&api_key={apiKey}" +
                              $"&timestamp={timestamp}" +
                              $"&signature={signature}";

            if (!string.IsNullOrEmpty(format))
                downloadUrl += $"&format={format}";

            return downloadUrl;
        }
        catch
        {
            return fileUrl;
        }
    }

    private static string ComputeSha1(string input)
    {
        var bytes = System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
