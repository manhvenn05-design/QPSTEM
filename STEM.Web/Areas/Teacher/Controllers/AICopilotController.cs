using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class AICopilotController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AICopilotController> _logger;
    private readonly IWebHostEnvironment _env;

    public AICopilotController(HttpClient httpClient, IConfiguration configuration, ILogger<AICopilotController> logger, IWebHostEnvironment env)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> RefineNote([FromBody] RefineNoteRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RawNote))
        {
            return Ok(new { success = false, message = "Ghi chú không được để trống hoặc dữ liệu gửi lên không hợp lệ." });
        }

        var rawNote = request.RawNote.Trim();
        if (rawNote.Length < 10)
        {
            return Ok(new { success = false, message = "Ghi chú quá ngắn. Thầy/Cô vui lòng nhập ít nhất 10 ký tự (nêu rõ tình trạng của học sinh) để AI có dữ liệu viết nhận xét." });
        }

        var apiKey = _configuration["GoogleAI:ApiKey"];
        var model = _configuration["GoogleAI:TextModel"] ?? "gemini-2.5-flash";

        if (string.IsNullOrEmpty(apiKey))
        {
            return Ok(new { success = false, message = "Hệ thống chưa được cấu hình API Key cho AI." });
        }

        var prompt = $@"Đóng vai là một chuyên gia tâm lý học đường và giao tiếp giáo dục.
Nhiệm vụ của bạn: Chuyển đổi 'ghi chú nháp' của giáo viên thành một đoạn nhận xét chuyên nghiệp, tinh tế và mang tính xây dựng để gửi trực tiếp cho PHỤ HUYNH.

[QUY TẮC CỐT LÕI - PHẢI TUÂN THỦ NGHIÊM NGẶT]:
1. KHÉO LÉO & TÍCH CỰC: Chuyển hóa hoàn toàn ngôn từ thô, tiêu cực thành ngôn từ sư phạm (khuyến khích sự phát triển).
   - 'Học dốt / kém / tệ' -> 'con đang gặp chút khó khăn và cần thêm thời gian để nắm vững kiến thức'.
   - 'Lười / nhác' -> 'con cần rèn luyện thêm sự chủ động và tính tự giác'.
   - 'Nghịch / ồn ào' -> 'con rất năng động, tuy nhiên cần chú ý tập trung hơn trong giờ học'.
2. ĐỘ DÀI & CẤU TRÚC: Khoảng 3-4 câu ngắn gọn, súc tích. Mở đầu bằng lời chào thân thiện (ví dụ: 'Kính gửi ba mẹ, hôm nay con...').
3. TRUNG THỰC NHƯNG TINH TẾ: Vẫn giữ nguyên ý nghĩa đánh giá (tốt/xấu) của giáo viên, không nịnh hót thái quá, chỉ làm cho câu văn dễ tiếp nhận hơn.
4. ĐỊNH DẠNG: Trả về trực tiếp nội dung hoàn chỉnh. Tuyệt đối KHÔNG giải thích, KHÔNG dùng dấu ngoặc kép bọc ngoài câu.

[GHI CHÚ NHÁP CỦA GIÁO VIÊN]:
""{rawNote}""";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.7
            },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        try
        {
            var response = await _httpClient.PostAsync(apiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Lỗi khi gọi Gemini API: {Response}", responseString);
                return Ok(new { success = false, message = $"Lỗi từ Google AI: {response.StatusCode} - {responseString}" });
            }

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
                    return Ok(new { success = true, suggestion = text });
                }
            }

            return Ok(new { success = false, message = "Không thể phân tích phản hồi từ AI." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception khi gọi Gemini API.");
            return Ok(new { success = false, message = $"Lỗi hệ thống khi gọi AI: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeVideo([FromBody] AnalyzeVideoRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            return Ok(new { success = false, message = "Đường dẫn video không hợp lệ. Vui lòng tải video lên trước." });
        }

        var apiKey = _configuration["GoogleAI:ApiKey"];
        var model = _configuration["GoogleAI:TextModel"] ?? "gemini-2.5-flash";

        if (string.IsNullOrEmpty(apiKey))
        {
            return Ok(new { success = false, message = "Hệ thống chưa được cấu hình API Key cho AI." });
        }

        var filePath = Path.Combine(_env.WebRootPath, request.VideoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(filePath))
        {
            return Ok(new { success = false, message = "Không tìm thấy file video trên máy chủ nội bộ." });
        }

        try
        {
            // 1. Upload file trực tiếp lên Google Gemini File API
            var uploadUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?uploadType=media&key={apiKey}";
            var fileContentBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileContentBytes);
            
            // Lấy MIME type động (tạm định mặc định mp4 nếu không có)
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = ext == ".mov" ? "video/quicktime" : (ext == ".avi" ? "video/x-msvideo" : "video/mp4");
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

            var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = fileContent };
            uploadRequest.Headers.Add("X-Goog-Upload-File-Name", Path.GetFileName(filePath));

            var uploadResponse = await _httpClient.SendAsync(uploadRequest);
            var uploadResponseString = await uploadResponse.Content.ReadAsStringAsync();

            if (!uploadResponse.IsSuccessStatusCode)
            {
                return Ok(new { success = false, message = "Lỗi khi đẩy video lên Google AI: " + uploadResponseString });
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
            for (int i = 0; i < 15; i++) // Tối đa chờ 30 giây
            {
                var checkResponse = await _httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={apiKey}");
                var checkResponseStr = await checkResponse.Content.ReadAsStringAsync();
                using var checkDoc = JsonDocument.Parse(checkResponseStr);
                var state = checkDoc.RootElement.GetProperty("state").GetString();

                if (state == "ACTIVE")
                {
                    isReady = true;
                    break;
                }
                if (state == "FAILED")
                {
                    return Ok(new { success = false, message = "Video bị lỗi khi xử lý bởi Google AI." });
                }
                await Task.Delay(2000); // Chờ 2s rồi check lại
            }

            if (!isReady)
            {
                return Ok(new { success = false, message = "Thời gian AI xem video quá lâu. Vui lòng thử lại." });
            }

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

            var generateUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var generateContent = new StringContent(JsonSerializer.Serialize(generatePayload), Encoding.UTF8, "application/json");
            var generateResponse = await _httpClient.PostAsync(generateUrl, generateContent);
            var generateResponseString = await generateResponse.Content.ReadAsStringAsync();

            if (!generateResponse.IsSuccessStatusCode)
            {
                return Ok(new { success = false, message = "Lỗi khi AI phân tích: " + generateResponseString });
            }

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
                    // Xóa markdown json nếu AI vẫn sinh ra
                    if (text!.StartsWith("```json")) {
                        text = text.Substring(7).Trim();
                        if (text.EndsWith("```")) text = text.Substring(0, text.Length - 3).Trim();
                    }
                    var analysisResult = JsonSerializer.Deserialize<JsonElement>(text);
                    return Ok(new { success = true, result = analysisResult });
                }
            }

            return Ok(new { success = false, message = "Không thể đọc kết quả từ AI." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception khi phân tích Video.");
            return Ok(new { success = false, message = $"Lỗi hệ thống khi gọi AI: {ex.Message}" });
        }
    }
}

public class RefineNoteRequest
{
    public string RawNote { get; set; } = string.Empty;
}

public class AnalyzeVideoRequest
{
    public string VideoUrl { get; set; } = string.Empty;
}
