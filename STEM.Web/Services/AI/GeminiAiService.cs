using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace STEM.Web.Services.AI;

/// <summary>
/// Dịch vụ gọi Google Gemini AI.
/// Chiến lược retry: KHÔNG retry khi 429 (quá tải) – trả lỗi ngay để người dùng biết.
/// Retry tối đa 1 lần với exponential backoff CHỈ cho lỗi mạng tạm thời (5xx, timeout).
/// Cache kết quả để tránh gọi API lặp lại cho cùng đầu vào.
/// </summary>
public class GeminiAiService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleAiOptions _options;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly IMemoryCache _cache;

    private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com";

    public GeminiAiService(
        HttpClient httpClient,
        IOptions<GoogleAiOptions> options,
        ILogger<GeminiAiService> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
        _httpClient.Timeout = TimeSpan.FromSeconds(
            _options.RequestTimeoutSeconds > 0 ? _options.RequestTimeoutSeconds : 120);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [1] Làm mượt ghi chú thô của giáo viên thành nhận xét chuyên nghiệp
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AIRequestResult<RefineNoteResult>> RefineTeacherNoteAsync(string rawNote)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return AIRequestResult<RefineNoteResult>.Fail("Hệ thống chưa cấu hình API Key.");

        // Cache theo hash nội dung để tránh gọi API nhiều lần cho cùng ghi chú
        var cacheKey = $"ai:refine:{ComputeHash(rawNote)}";
        if (_cache.TryGetValue(cacheKey, out RefineNoteResult? cached) && cached != null)
            return AIRequestResult<RefineNoteResult>.Ok(cached);

        var prompt = $"""
            Đóng vai là chuyên gia tâm lý học trường và giao tiếp giáo dục.
            Nhiệm vụ: Chuyển đổi 'ghi chú nháp' của giáo viên thành nhận xét chuyên nghiệp, tinh tế và xây dựng để gửi cho PHỤ HUYNH.

            QUY TẮC:
            1. KHÉO LÉO & TÍCH CỰC: Chuyển hóa ngôn từ thô, tiêu cực thành ngôn từ sư phạm.
            2. ĐỘ DÀI: Khoảng 3-4 câu, súc tích, mở đầu thân thiện.
            3. TRUNG THỰC: Vẫn giữ ý nghĩa đánh giá gốc của giáo viên.
            4. ĐỊNH DẠNG: Trả về trực tiếp nội dung. KHÔNG giải thích, KHÔNG ngoặc kép bọc ngoài.

            GHI CHÚ NHÁP:
            "{rawNote}"
            """;

        var payload = BuildTextPayload(prompt, temperature: 0.7);
        var url = BuildGenerateUrl();

        try
        {
            var json = await CallWithRetryAsync(url, payload);
            var text = ExtractTextFromResponse(json);
            if (text == null)
                return AIRequestResult<RefineNoteResult>.Fail("Không thể phân tích phản hồi từ AI.");

            var result = new RefineNoteResult { Suggestion = text };
            _cache.Set(cacheKey, result, TimeSpan.FromHours(2));
            return AIRequestResult<RefineNoteResult>.Ok(result);
        }
        catch (GeminiRateLimitException)
        {
            return AIRequestResult<RefineNoteResult>.Fail("AI đang bận, vui lòng thử lại sau ít phút.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi HTTP khi gọi AI RefineNote.");
            return AIRequestResult<RefineNoteResult>.Fail(MapHttpError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi gọi AI RefineNote.");
            return AIRequestResult<RefineNoteResult>.Fail("Có lỗi xảy ra, vui lòng thử lại sau.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [2] Phân tích video thuyết trình của học sinh
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AIRequestResult<VideoAnalysisResult>> AnalyzePresentationVideoAsync(
        string absoluteFilePath, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return AIRequestResult<VideoAnalysisResult>.Fail("Hệ thống chưa cấu hình API Key.");

        if (!File.Exists(absoluteFilePath))
            return AIRequestResult<VideoAnalysisResult>.Fail("Không tìm thấy file video.");

        // Cache theo tên file + kích thước để nhận diện file đã xử lý
        var fileInfo = new FileInfo(absoluteFilePath);
        var cacheKey = $"ai:video:{fileInfo.Name}:{fileInfo.Length}";
        if (_cache.TryGetValue(cacheKey, out VideoAnalysisResult? cached) && cached != null)
            return AIRequestResult<VideoAnalysisResult>.Ok(cached);

        try
        {
            // BƯỚC 1: Upload file lên Google Files API
            var (fileUri, fileName) = await UploadVideoAsync(absoluteFilePath, mimeType);

            // BƯỚC 2: Poll cho đến khi file sẵn sàng (ACTIVE)
            var ready = await WaitForFileReadyAsync(fileName);
            if (!ready)
                return AIRequestResult<VideoAnalysisResult>.Fail("AI xử lý video quá lâu. Vui lòng thử lại.");

            // BƯỚC 3: Gọi generateContent để phân tích
            var result = await AnalyzeVideoContentAsync(fileUri, mimeType);
            if (result.Success && result.Data != null)
                _cache.Set(cacheKey, result.Data, TimeSpan.FromHours(12));

            return result;
        }
        catch (GeminiRateLimitException)
        {
            return AIRequestResult<VideoAnalysisResult>.Fail(
                "AI đang bận (quá tải). Vui lòng chờ 1-2 phút rồi thử lại.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi HTTP khi phân tích video.");
            return AIRequestResult<VideoAnalysisResult>.Fail(MapHttpError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi phân tích video.");
            return AIRequestResult<VideoAnalysisResult>.Fail($"Lỗi hệ thống: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE – Upload & Poll
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<(string fileUri, string fileName)> UploadVideoAsync(
        string filePath, string mimeType)
    {
        var uploadUrl = $"{GeminiBaseUrl}/upload/v1beta/files?uploadType=media&key={_options.ApiKey}";
        var fileBytes = await File.ReadAllBytesAsync(filePath);

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

        var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = fileContent };
        request.Headers.Add("X-Goog-Upload-File-Name", Path.GetFileName(filePath));

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Upload video thất bại ({Status}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Upload thất bại: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        var fileObj = doc.RootElement.GetProperty("file");
        return (
            fileObj.GetProperty("uri").GetString()!,
            fileObj.GetProperty("name").GetString()!
        );
    }

    private async Task<bool> WaitForFileReadyAsync(string fileName)
    {
        // Chờ 8s trước, sau đó poll mỗi 5s, tối đa 15 lần (~83s tổng)
        await Task.Delay(8000);

        for (int i = 0; i < 15; i++)
        {
            var checkUrl = $"{GeminiBaseUrl}/v1beta/{fileName}?key={_options.ApiKey}";
            var resp = await _httpClient.GetAsync(checkUrl);
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var state = doc.RootElement.TryGetProperty("state", out var sp)
                ? sp.GetString() : null;

            if (state == "ACTIVE") return true;
            if (state == "FAILED")
                throw new InvalidOperationException("Google Files API báo FAILED khi xử lý video.");

            await Task.Delay(5000);
        }

        return false;
    }

    private async Task<AIRequestResult<VideoAnalysisResult>> AnalyzeVideoContentAsync(
        string fileUri, string mimeType)
    {
        var prompt = """
            Bạn là chuyên gia giáo dục STEM đang xem xét video được tải lên bởi giáo viên.

            BƯỚC 1 – XÁC MINH: Đây có phải video học sinh thuyết trình / trình bày sản phẩm STEM không?
            HỢP LỆ: học sinh nói chuyện, giới thiệu sản phẩm/dự án, demo robot/mô hình, giải thích ý tưởng.
            KHÔNG HỢP LỆ: phim, clip nhạc, video ngẫu nhiên, không liên quan học tập STEM.

            BƯỚC 2 – ĐÁNH GIÁ (chỉ khi HỢP LỆ):
            - 2 điểm mạnh nổi bật (nội dung, phát âm, sáng tạo, tự tin).
            - 1-2 điểm cần cải thiện.
            - Điểm số /100.
            - 1 câu nhận xét ngắn gọn, khích lệ gửi phụ huynh.

            TRẢ VỀ JSON DUY NHẤT (không thêm gì khác):
            Khi HỢP LỆ:   {"is_valid":true,"score":"85/100","strengths":["...","..."],"weaknesses":["..."],"suggestion":"..."}
            Khi KHÔNG HỢP LỆ: {"is_valid":false,"reason":"..."}
            """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { fileData = new { mimeType, fileUri } },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new { temperature = 0.4, responseMimeType = "application/json" }
        };

        var url = BuildGenerateUrl();
        var responseJson = await CallWithRetryAsync(url, payload);

        // Parse candidates
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return AIRequestResult<VideoAnalysisResult>.Fail("AI không trả về kết quả. Vui lòng thử lại.");

        var textRaw = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()?.Trim() ?? "";

        // Strip markdown code fences nếu có
        if (textRaw.StartsWith("```"))
        {
            var firstNewline = textRaw.IndexOf('\n');
            if (firstNewline >= 0) textRaw = textRaw[(firstNewline + 1)..];
            if (textRaw.EndsWith("```")) textRaw = textRaw[..^3].Trim();
        }

        using var resultDoc = JsonDocument.Parse(textRaw);
        var resultRoot = resultDoc.RootElement;

        // Kiểm tra is_valid
        if (resultRoot.TryGetProperty("is_valid", out var isValidProp) && !isValidProp.GetBoolean())
        {
            var reason = resultRoot.TryGetProperty("reason", out var r)
                ? r.GetString() : "Video không phải thuyết trình của học sinh.";
            return AIRequestResult<VideoAnalysisResult>.Fail($"Video không hợp lệ: {reason}");
        }

        return AIRequestResult<VideoAnalysisResult>.Ok(VideoAnalysisResult.FromJson(resultRoot));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE – HTTP Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi API với 1 lần retry cho lỗi mạng/server (5xx).
    /// KHÔNG retry khi 429 – ném GeminiRateLimitException để caller báo ngay cho user.
    /// </summary>
    private async Task<string> CallWithRetryAsync(string url, object payload)
    {
        for (int attempt = 0; attempt <= 1; attempt++)
        {
            try
            {
                return await PostJsonAsync(url, payload);
            }
            catch (HttpRequestException ex) when (
                attempt == 0 && ex.StatusCode != null &&
                (int)ex.StatusCode.Value >= 500)
            {
                // Server error tạm thời → thử lại 1 lần sau 3s
                _logger.LogWarning("Gemini trả về {Status}, thử lại sau 3s...", ex.StatusCode);
                await Task.Delay(3000);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // 429 → KHÔNG retry, throw ngay
                _logger.LogWarning("Gemini 429 – Rate limit exceeded.");
                throw new GeminiRateLimitException();
            }
        }

        // Lần thử cuối không được bắt exception
        return await PostJsonAsync(url, payload);
    }

    private async Task<string> PostJsonAsync(string url, object payload)
    {
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, body);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode}: {responseText}", null, response.StatusCode);

        return responseText;
    }

    private string? ExtractTextFromResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        return candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()?.Trim();
    }

    private static object BuildTextPayload(string prompt, double temperature = 0.7) => new
    {
        contents = new[] { new { parts = new[] { new { text = prompt } } } },
        generationConfig = new { temperature }
    };

    private string BuildGenerateUrl() =>
        $"{GeminiBaseUrl}/v1beta/models/{_options.TextModel}:generateContent?key={_options.ApiKey}";

    private static string MapHttpError(HttpRequestException ex) => ex.StatusCode switch
    {
        HttpStatusCode.Forbidden => "API Key không hợp lệ hoặc đã bị chặn.",
        HttpStatusCode.TooManyRequests => "AI đang bận. Vui lòng thử lại sau ít phút.",
        null => "Không thể kết nối đến AI (lỗi mạng).",
        _ => $"Lỗi từ AI ({(int)ex.StatusCode!})."
    };

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class GeminiRateLimitException : Exception { }
}
