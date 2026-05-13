using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace STEM.Web.Services.AI;

public class GeminiAiService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly GoogleAiOptions _options;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly IMemoryCache _cache;

    public GeminiAiService(HttpClient httpClient, IOptions<GoogleAiOptions> options, ILogger<GeminiAiService> logger, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds > 0 ? _options.RequestTimeoutSeconds : 30);
    }

    public async Task<AIRequestResult<RefineNoteResult>> RefineTeacherNoteAsync(string rawNote)
    {
        var cacheKey = $"ai_refinenote_{GetSha256Hash(rawNote)}";
        if (_cache.TryGetValue(cacheKey, out RefineNoteResult? cachedResult) && cachedResult != null)
        {
            return AIRequestResult<RefineNoteResult>.Ok(cachedResult);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return AIRequestResult<RefineNoteResult>.Fail("Hệ thống chưa cấu hình API Key.");
        }

        var prompt = $@"Đóng vai là một chuyên gia tâm lý học trường và giao tiếp giáo dục.
Nhiệm vụ của bạn: Chuyển đổi 'ghi chú nháp' của giáo viên thành một đoạn nhận xét chuyên nghiệp, tinh tế và mang tính xây dựng để gửi trực tiếp cho PHỤ HUYNH.

[QUY TẮC CỐT LÕI - PHẢI TUÂN THỦ NGHIÊM NGẶT]:
1. KHÉO LÉO & TÍCH CỰC: Chuyển hóa hoàn toàn ngôn từ thô, tiêu cực thành ngôn từ sư phạm (khuyến khích sự phát triển).
2. ĐỘ DÀI & CẤU TRÚC: Khoảng 3-4 câu ngắn gọn, súc tích. Mở đầu bằng lời chào thân thiện.
3. TRUNG THỰC NHƯNG TINH TẾ: Vẫn giữ nguyên ý nghĩa đánh giá của giáo viên.
4. ĐỊNH DẠNG: Trả về trực tiếp nội dung hoàn chỉnh. KHÔNG giải thích, KHÔNG dùng ngoặc kép bọc ngoài câu.

[GHI CHÚ NHÁP CỦA GIÁO VIÊN]:
""{rawNote}""";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.7 },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.TextModel}:generateContent?key={_options.ApiKey}";
        
        try
        {
            var responseString = await ExecuteWithRetryAsync(() => SendPostAsync(apiUrl, payload));
            using var document = JsonDocument.Parse(responseString);
            var root = document.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var candidateContent) && 
                    candidateContent.TryGetProperty("parts", out var parts) && 
                    parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString()?.Trim();
                    var result = new RefineNoteResult { Suggestion = text ?? "" };
                    _cache.Set(cacheKey, result, TimeSpan.FromHours(2));
                    return AIRequestResult<RefineNoteResult>.Ok(result);
                }
            }

            return AIRequestResult<RefineNoteResult>.Fail("Không thể phân tích phản hồi từ AI.");
        }
        catch (HttpRequestException ex)
        {
            return AIRequestResult<RefineNoteResult>.Fail(MapHttpError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi gọi AI (Refine Note).");
            return AIRequestResult<RefineNoteResult>.Fail("Có lỗi xảy ra khi gọi dịch vụ AI. Vui lòng thử lại sau.");
        }
    }

    public async Task<AIRequestResult<VideoAnalysisResult>> AnalyzePresentationVideoAsync(string absoluteFilePath, string mimeType)
    {
        var fileInfo = new FileInfo(absoluteFilePath);
        var cacheKey = $"ai_video_{fileInfo.Name}_{fileInfo.Length}";
        if (_cache.TryGetValue(cacheKey, out VideoAnalysisResult? cachedResult) && cachedResult != null)
        {
            return AIRequestResult<VideoAnalysisResult>.Ok(cachedResult);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return AIRequestResult<VideoAnalysisResult>.Fail("Hệ thống chưa cấu hình API Key.");
        }

        try
        {
            // 1. Upload
            var uploadUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?uploadType=media&key={_options.ApiKey}";
            var fileBytes = await File.ReadAllBytesAsync(absoluteFilePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

            var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = fileContent };
            uploadRequest.Headers.Add("X-Goog-Upload-File-Name", Path.GetFileName(absoluteFilePath));

            var uploadResponse = await _httpClient.SendAsync(uploadRequest);
            var uploadResponseString = await uploadResponse.Content.ReadAsStringAsync();

            if (!uploadResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Lỗi upload video: {Response}", uploadResponseString);
                return AIRequestResult<VideoAnalysisResult>.Fail("Không thể tải video lên AI xử lý.");
            }

            string fileUri = "";
            string fileName = "";
            using (var doc = JsonDocument.Parse(uploadResponseString))
            {
                var fileObj = doc.RootElement.GetProperty("file");
                fileUri = fileObj.GetProperty("uri").GetString()!;
                fileName = fileObj.GetProperty("name").GetString()!;
            }

            // 2. Poll đợi file được xử lý xong
            bool isReady = false;
            for (int i = 0; i < 15; i++)
            {
                var checkResponse = await _httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={_options.ApiKey}");
                var checkResponseStr = await checkResponse.Content.ReadAsStringAsync();
                using var checkDoc = JsonDocument.Parse(checkResponseStr);
                var state = checkDoc.RootElement.GetProperty("state").GetString();

                if (state == "ACTIVE") { isReady = true; break; }
                if (state == "FAILED") return AIRequestResult<VideoAnalysisResult>.Fail("Video bị lỗi khi xử lý bởi hệ thống AI.");
                await Task.Delay(2000);
            }

            if (!isReady) return AIRequestResult<VideoAnalysisResult>.Fail("Thời gian AI xem video quá lâu. Vui lòng thử lại.");

            // 3. Phân tích nội dung Video
            var prompt = @"Đóng vai là một chuyên gia giáo dục STEM. Hãy phân tích video thuyết trình sản phẩm của học sinh và đánh giá:
1. ĐIỂM SÁNG: Nêu 2 ưu điểm nổi bật về nội dung, phát âm, sự sáng tạo hoặc tự tin.
2. CẦN CẢI THIỆN: Nêu 1-2 điểm cần khắc phục để làm tốt hơn.
3. ĐIỂM SỐ: Đánh giá trên thang điểm 100 (Ví dụ: 85/100).
4. GỢI Ý NHẬN XÉT: Viết một câu nhận xét tổng quát ngắn gọn, khích lệ để gửi cho phụ huynh.

TRẢ VỀ KẾT QUẢ DƯỚI ĐỊNH DẠNG JSON THEO CẤU TRÚC SAU:
{
  ""score"": ""85/100"",
  ""strengths"": [""Điểm 1"", ""Điểm 2""],
  ""weaknesses"": [""Cần cải thiện 1""],
  ""suggestion"": ""Nhận xét gửi phụ huynh""
}";

            var generatePayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { fileData = new { mimeType = mimeType, fileUri = fileUri } },
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new { temperature = 0.5, responseMimeType = "application/json" }
            };

            var generateUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.TextModel}:generateContent?key={_options.ApiKey}";
            var generateResponseString = await ExecuteWithRetryAsync(() => SendPostAsync(generateUrl, generatePayload));

            using var genDoc = JsonDocument.Parse(generateResponseString);
            var root = genDoc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var candidateContent) && 
                    candidateContent.TryGetProperty("parts", out var parts) && 
                    parts.GetArrayLength() > 0)
                {
                    var text = parts[0].GetProperty("text").GetString()?.Trim();
                    if (text!.StartsWith("```json")) {
                        text = text.Substring(7).Trim();
                        if (text.EndsWith("```")) text = text.Substring(0, text.Length - 3).Trim();
                    }
                    var analysisResult = VideoAnalysisResult.FromJson(JsonDocument.Parse(text).RootElement);
                    _cache.Set(cacheKey, analysisResult, TimeSpan.FromHours(12));
                    return AIRequestResult<VideoAnalysisResult>.Ok(analysisResult);
                }
            }

            return AIRequestResult<VideoAnalysisResult>.Fail("Không thể đọc kết quả từ AI.");
        }
        catch (HttpRequestException ex)
        {
            return AIRequestResult<VideoAnalysisResult>.Fail(MapHttpError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi phân tích Video.");
            return AIRequestResult<VideoAnalysisResult>.Fail("Có lỗi xảy ra khi gọi dịch vụ AI. Vui lòng thử lại sau.");
        }
    }

    private async Task<string> SendPostAsync(string url, object payload)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        
        var responseString = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            throw new HttpRequestException($"Lỗi HTTP {statusCode}: {responseString}", null, response.StatusCode);
        }
        return responseString;
    }

    private async Task<string> ExecuteWithRetryAsync(Func<Task<string>> action)
    {
        int maxRetries = _options.MaxRetryCount > 0 ? _options.MaxRetryCount : 3;
        int delayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (ShouldRetry(ex) && i < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Gọi AI thất bại (lần {Attempt}). Thử lại sau {Delay}ms...", i + 1, delayMs);
                await Task.Delay(delayMs);
                delayMs *= 2; // Exponential backoff
            }
        }

        return await action(); // Lần thử cuối cùng
    }

    private bool ShouldRetry(HttpRequestException ex)
    {
        if (ex.StatusCode == null) return true; // Lỗi mạng (timeout, ko kết nối được) -> Retry
        int code = (int)ex.StatusCode.Value;
        // Không retry với 400 (Bad Request), 401 (Unauthorized), 403 (Forbidden)
        if (code == 400 || code == 401 || code == 403) return false;
        return true;
    }

    private string MapHttpError(HttpRequestException ex)
    {
        if (ex.StatusCode == System.Net.HttpStatusCode.Forbidden) return "API Key không hợp lệ hoặc đã bị chặn.";
        if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests) return "Hệ thống AI đang quá tải. Vui lòng chờ một chút.";
        return "Kết nối đến AI bị lỗi.";
    }

    private string GetSha256Hash(string rawData)
    {
        using var sha256Hash = SHA256.Create();
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var t in bytes) builder.Append(t.ToString("x2"));
        return builder.ToString();
    }
}
