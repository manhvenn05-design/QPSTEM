using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace STEM.Web.Services.AI;

/// <summary>
/// Dịch vụ gọi Google Gemini AI.
/// Chiến lược retry: KHÔNG retry khi 429 – trả lỗi ngay để người dùng biết.
/// Retry tối đa 1 lần với exponential backoff CHỈ cho lỗi mạng tạm thời (5xx).
/// Cache kết quả để tránh gọi API lặp lại cho cùng đầu vào.
/// </summary>
public class GeminiAiService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleAiOptions _options;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly IMemoryCache _cache;

    private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com";

    // Poll config: chờ 6s đầu, poll mỗi 6s, tối đa 10 lần → tổng ~66s
    private const int PollInitialDelayMs = 6_000;
    private const int PollIntervalMs = 6_000;
    private const int PollMaxAttempts = 10;

    private static readonly Dictionary<string, string> KnownMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".mp4",  "video/mp4" },
        { ".m4v",  "video/mp4" },
        { ".mov",  "video/quicktime" },
        { ".avi",  "video/x-msvideo" },
        { ".webm", "video/webm" },
        { ".mkv",  "video/x-matroska" },
    };

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

        // Timeout tổng: đủ cho upload + poll + analysis (180s)
        _httpClient.Timeout = TimeSpan.FromSeconds(180);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [1] Làm mượt ghi chú thô của giáo viên thành nhận xét chuyên nghiệp
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AIRequestResult<RefineNoteResult>> RefineTeacherNoteAsync(
        string rawNote, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return AIRequestResult<RefineNoteResult>.Fail("Hệ thống chưa cấu hình API Key.");

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
            var json = await CallWithRetryAsync(url, payload, ct);
            var text = ExtractTextFromResponse(json);
            if (text == null)
                return AIRequestResult<RefineNoteResult>.Fail("Không thể phân tích phản hồi từ AI.");

            var result = new RefineNoteResult { Suggestion = text };
            _cache.Set(cacheKey, result, TimeSpan.FromHours(2));
            return AIRequestResult<RefineNoteResult>.Ok(result);
        }
        catch (GeminiRateLimitException ex)
        {
            return AIRequestResult<RefineNoteResult>.Fail(BuildRateLimitUserMessage(ex));
        }
        catch (OperationCanceledException)
        {
            return AIRequestResult<RefineNoteResult>.Fail("Yêu cầu bị hủy.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[AI:RefineNote] Lỗi HTTP. Status={Status}", ex.StatusCode);
            return AIRequestResult<RefineNoteResult>.Fail(MapHttpError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI:RefineNote] Lỗi hệ thống không xác định.");
            return AIRequestResult<RefineNoteResult>.Fail("Có lỗi xảy ra, vui lòng thử lại sau.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [2] Phân tích video thuyết trình của học sinh
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<AIRequestResult<VideoAnalysisResult>> AnalyzePresentationVideoAsync(
        string absoluteFilePath, string mimeType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return AIRequestResult<VideoAnalysisResult>.Fail("Hệ thống chưa cấu hình API Key.");

        if (!File.Exists(absoluteFilePath))
            return AIRequestResult<VideoAnalysisResult>.Fail("Không tìm thấy file video.");

        // Ghi đè mimeType bằng detection chính xác theo extension
        var ext = Path.GetExtension(absoluteFilePath);
        if (KnownMimeTypes.TryGetValue(ext, out var detectedMime))
            mimeType = detectedMime;

        var fileInfo = new FileInfo(absoluteFilePath);
        var cacheKey = $"ai:video:{fileInfo.Name}:{fileInfo.Length}";
        if (_cache.TryGetValue(cacheKey, out VideoAnalysisResult? cached) && cached != null)
            return AIRequestResult<VideoAnalysisResult>.Ok(cached);

        _logger.LogInformation("[AI:Video] Bắt đầu phân tích: {File} ({Size}KB, {Mime})",
            fileInfo.Name, fileInfo.Length / 1024, mimeType);

        try
        {
            // BƯỚC 1: Upload file lên Google Files API
            var (fileUri, fileName) = await UploadVideoAsync(absoluteFilePath, mimeType, ct);
            _logger.LogInformation("[AI:Video] Upload thành công → {FileName}", fileName);

            // BƯỚC 2: Poll cho đến khi file sẵn sàng (ACTIVE)
            var ready = await WaitForFileReadyAsync(fileName, ct);
            if (!ready)
                return AIRequestResult<VideoAnalysisResult>.Fail("AI xử lý video quá lâu. Vui lòng thử lại.");

            _logger.LogInformation("[AI:Video] File ACTIVE, bắt đầu phân tích nội dung.");

            // BƯỚC 3: Gọi generateContent để phân tích
            var result = await AnalyzeVideoContentAsync(fileUri, mimeType, ct);
            if (result.Success && result.Data != null)
                _cache.Set(cacheKey, result.Data, TimeSpan.FromHours(12));

            return result;
        }
        catch (GeminiRateLimitException ex)
        {
            return AIRequestResult<VideoAnalysisResult>.Fail(BuildRateLimitUserMessage(ex));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout (không phải user cancel)
            _logger.LogError(ex, "[AI:Video] HttpClient timeout khi xử lý video.");
            return AIRequestResult<VideoAnalysisResult>.Fail(
                "Quá thời gian chờ AI xử lý video. Vui lòng thử lại với video ngắn hơn.");
        }
        catch (OperationCanceledException)
        {
            return AIRequestResult<VideoAnalysisResult>.Fail("Yêu cầu bị hủy.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[AI:Video] Lỗi HTTP. Status={Status}", ex.StatusCode);
            return AIRequestResult<VideoAnalysisResult>.Fail(MapHttpError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI:Video] Lỗi hệ thống: {Msg}", ex.Message);
            return AIRequestResult<VideoAnalysisResult>.Fail($"Lỗi hệ thống: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE – Upload & Poll
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<(string fileUri, string fileName)> UploadVideoAsync(
        string filePath, string mimeType, CancellationToken ct)
    {
        var uploadUrl = $"{GeminiBaseUrl}/upload/v1beta/files?uploadType=media&key={_options.ApiKey}";
        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);

        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

        var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = fileContent };
        request.Headers.Add("X-Goog-Upload-File-Name", Path.GetFileName(filePath));

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            int? retrySeconds = TryParseRetrySeconds(body);
            bool isQuotaExhausted = IsQuotaExhausted(body);

            _logger.LogWarning("[AI:Upload] 429 - {Kind}. RetryAfter={Retry}s",
                isQuotaExhausted ? "Quota exhausted" : "Rate limited", retrySeconds);

            throw new GeminiRateLimitException(retrySeconds, isQuotaExhausted);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[AI:Upload] Thất bại ({Status}): {Body}", response.StatusCode,
                body.Length > 500 ? body[..500] : body);
            throw new HttpRequestException($"Upload thất bại: {response.StatusCode}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);
        var fileObj = doc.RootElement.GetProperty("file");
        return (
            fileObj.GetProperty("uri").GetString()!,
            fileObj.GetProperty("name").GetString()!
        );
    }

    private async Task<bool> WaitForFileReadyAsync(string fileName, CancellationToken ct)
    {
        await Task.Delay(PollInitialDelayMs, ct);

        for (int i = 0; i < PollMaxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();

            var checkUrl = $"{GeminiBaseUrl}/v1beta/{fileName}?key={_options.ApiKey}";
            var resp = await _httpClient.GetAsync(checkUrl, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var state = doc.RootElement.TryGetProperty("state", out var sp)
                ? sp.GetString() : null;

            _logger.LogDebug("[AI:Poll] attempt={I}, state={State}", i + 1, state);

            if (state == "ACTIVE") return true;
            if (state == "FAILED")
                throw new InvalidOperationException("Google Files API báo FAILED khi xử lý video.");

            await Task.Delay(PollIntervalMs, ct);
        }

        return false;
    }

    private async Task<AIRequestResult<VideoAnalysisResult>> AnalyzeVideoContentAsync(
        string fileUri, string mimeType, CancellationToken ct)
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
        var responseJson = await CallWithRetryAsync(url, payload, ct);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return AIRequestResult<VideoAnalysisResult>.Fail("AI không trả về kết quả. Vui lòng thử lại.");

        var candidate = candidates[0];

        // Kiểm tra finishReason để phát hiện lỗi content filter, etc.
        if (candidate.TryGetProperty("finishReason", out var finishReason))
        {
            var reason = finishReason.GetString();
            if (reason is "SAFETY" or "RECITATION" or "OTHER")
            {
                _logger.LogWarning("[AI:Video] finishReason={Reason}", reason);
                return AIRequestResult<VideoAnalysisResult>.Fail("AI không thể phân tích video này.");
            }
        }

        var textRaw = candidate
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

        try
        {
            using var resultDoc = JsonDocument.Parse(textRaw);
            var resultRoot = resultDoc.RootElement;

            if (resultRoot.TryGetProperty("is_valid", out var isValidProp) && !isValidProp.GetBoolean())
            {
                var reason = resultRoot.TryGetProperty("reason", out var r)
                    ? r.GetString() : "Video không phải thuyết trình của học sinh.";
                return AIRequestResult<VideoAnalysisResult>.Fail($"Video không hợp lệ: {reason}");
            }

            return AIRequestResult<VideoAnalysisResult>.Ok(VideoAnalysisResult.FromJson(resultRoot));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[AI:Video] Không parse được JSON response: {Raw}",
                textRaw.Length > 200 ? textRaw[..200] : textRaw);
            return AIRequestResult<VideoAnalysisResult>.Fail("AI trả về định dạng không hợp lệ. Vui lòng thử lại.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRIVATE – HTTP Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retry tối đa 1 lần cho lỗi 5xx.
    /// 429 → throw GeminiRateLimitException ngay (không retry).
    /// Lần cuối (attempt=1) → để exception tự nhiên propagate.
    /// </summary>
    private async Task<string> CallWithRetryAsync(string url, object payload, CancellationToken ct)
    {
        HttpRequestException? lastEx = null;

        for (int attempt = 0; attempt <= 1; attempt++)
        {
            try
            {
                return await PostJsonAsync(url, payload, ct);
            }
            catch (GeminiRateLimitException)
            {
                throw; // Không retry 429
            }
            catch (HttpRequestException ex) when (
                attempt == 0 &&
                ex.StatusCode != null &&
                (int)ex.StatusCode.Value >= 500)
            {
                // Server error tạm thời → thử lại 1 lần sau 3s
                lastEx = ex;
                _logger.LogWarning("[AI:Retry] Gemini {Status}, thử lại sau 3s...", ex.StatusCode);
                await Task.Delay(3000, ct);
            }
        }

        // Lần thử cuối – không catch, để exception propagate tự nhiên
        try { return await PostJsonAsync(url, payload, ct); }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[AI:Retry] Thất bại sau {Attempts} lần thử.", 2);
            throw;
        }
    }

    private async Task<string> PostJsonAsync(string url, object payload, CancellationToken ct)
    {
        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, body, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Parse thời gian retry từ message của Gemini: "Please retry in 37.87s"
            int? retrySeconds = TryParseRetrySeconds(responseText);
            bool isQuotaExhausted = IsQuotaExhausted(responseText);

            _logger.LogWarning("[AI] 429 – {Kind}. RetryAfter={Retry}s",
                isQuotaExhausted ? "Quota exhausted" : "Rate limited", retrySeconds);

            throw new GeminiRateLimitException(retrySeconds, isQuotaExhausted);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[AI] HTTP {Status}: {Body}", (int)response.StatusCode,
                responseText.Length > 300 ? responseText[..300] : responseText);
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode}: {responseText}", null, response.StatusCode);
        }

        return responseText;
    }

    private static bool IsQuotaExhausted(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        return body.Contains("limit: 0", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("resource_exhausted", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Tìm "Please retry in X.XXs" trong body Gemini 429.</summary>
    private static int? TryParseRetrySeconds(string body)
    {
        const string marker = "Please retry in ";
        var idx = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var after = body[(idx + marker.Length)..];
        var end = after.IndexOfAny(['s', ' ', '"', '\n']);
        if (end <= 0) return null;
        if (double.TryParse(after[..end],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var secs))
        {
            return (int)Math.Ceiling(secs);
        }
        return null;
    }

    private string? ExtractTextFromResponse(string json)
    {
        try
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
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[AI] Không parse được response JSON.");
            return null;
        }
    }

    private static object BuildTextPayload(string prompt, double temperature = 0.7) => new
    {
        contents = new[] { new { parts = new[] { new { text = prompt } } } },
        generationConfig = new { temperature }
    };

    private string BuildGenerateUrl() =>
        $"{GeminiBaseUrl}/v1beta/models/{_options.TextModel}:generateContent?key={_options.ApiKey}";

    private static string BuildRateLimitUserMessage(GeminiRateLimitException ex)
    {
        if (ex.IsQuotaExhausted)
        {
            return "Hệ thống AI đã chạm hạn mức sử dụng của project hiện tại. Nếu đang dùng free tier, thường phải chờ quota reset hoặc bật billing; đổi API key mới trong cùng project thường không giải quyết được.";
        }

        if (ex.RetrySeconds.HasValue)
        {
            return $"AI đang bận do chạm giới hạn tạm thời của Gemini. Vui lòng thử lại sau {ex.RetrySeconds} giây.";
        }

        return "AI đang bận do chạm giới hạn tạm thời của Gemini. Vui lòng thử lại sau ít phút.";
    }

    private static string MapHttpError(HttpRequestException ex) => ex.StatusCode switch
    {
        HttpStatusCode.Unauthorized => "API Key không hợp lệ hoặc hết hạn.",
        HttpStatusCode.Forbidden => "API Key bị chặn hoặc không có quyền truy cập.",
        HttpStatusCode.TooManyRequests => "AI đang bận. Vui lòng thử lại sau ít phút.",
        null => "Không thể kết nối đến AI (lỗi mạng).",
        _ => $"Lỗi từ AI ({(int)ex.StatusCode!})."
    };

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class GeminiRateLimitException : Exception
    {
        public int? RetrySeconds { get; }
        public bool IsQuotaExhausted { get; }

        public GeminiRateLimitException(int? retrySeconds = null, bool isQuotaExhausted = false)
        {
            RetrySeconds = retrySeconds;
            IsQuotaExhausted = isQuotaExhausted;
        }

        public string UserMessage()
        {
            if (IsQuotaExhausted)
                return "Hệ thống AI đã hết hạn mức sử dụng miễn phí trong ngày hôm nay. Vui lòng thử lại vào ngày mai.";

            if (RetrySeconds.HasValue)
                return $"AI đang bận, vui lòng thử lại sau {RetrySeconds} giây.";

            return "AI đang bận, vui lòng thử lại sau ít phút.";
        }
    }
}
