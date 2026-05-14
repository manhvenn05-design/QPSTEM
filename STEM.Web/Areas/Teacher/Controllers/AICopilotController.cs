using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STEM.Web.Services.AI;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class AICopilotController : Controller
{
    private readonly IAIService _aiService;
    private readonly ILogger<AICopilotController> _logger;
    private readonly IWebHostEnvironment _env;

    private static readonly Dictionary<string, string> MimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".mp4",  "video/mp4" },
        { ".m4v",  "video/mp4" },
        { ".mov",  "video/quicktime" },
        { ".avi",  "video/x-msvideo" },
        { ".webm", "video/webm" },
        { ".mkv",  "video/x-matroska" },
    };

    public AICopilotController(
        IAIService aiService,
        ILogger<AICopilotController> logger,
        IWebHostEnvironment env)
    {
        _aiService = aiService;
        _logger = logger;
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> RefineNote(
        [FromBody] RefineNoteRequest? request,
        CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RawNote))
            return Ok(new { success = false, message = "Ghi chú không được để trống." });

        var rawNote = request.RawNote.Trim();
        if (rawNote.Length < 10)
            return Ok(new { success = false, message = "Ghi chú quá ngắn. Vui lòng nhập ít nhất 10 ký tự để AI có cơ sở viết nhận xét." });

        var result = await _aiService.RefineTeacherNoteAsync(rawNote, ct);

        return result.Success
            ? Ok(new { success = true, suggestion = result.Data?.Suggestion })
            : Ok(new { success = false, message = result.Message });
    }

    [HttpPost]
    public async Task<IActionResult> AnalyzeVideo(
        [FromBody] AnalyzeVideoRequest? request,
        CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.VideoUrl))
            return Ok(new { success = false, message = "Đường dẫn video không hợp lệ." });

        var relativePath = request.VideoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_env.WebRootPath, relativePath);

        if (!System.IO.File.Exists(filePath))
            return Ok(new { success = false, message = "Không tìm thấy file video trên máy chủ." });

        var ext = Path.GetExtension(filePath);
        if (!MimeMap.TryGetValue(ext, out var mimeType))
            return Ok(new { success = false, message = $"Định dạng video '{ext}' không được hỗ trợ." });

        var result = await _aiService.AnalyzePresentationVideoAsync(filePath, mimeType, ct);

        return result.Success
            ? Ok(new { success = true, result = result.Data })
            : Ok(new { success = false, message = result.Message });
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
