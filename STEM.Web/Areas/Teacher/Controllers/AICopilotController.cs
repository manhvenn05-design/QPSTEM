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

    public AICopilotController(IAIService aiService, ILogger<AICopilotController> logger, IWebHostEnvironment env)
    {
        _aiService = aiService;
        _logger = logger;
        _env = env;
    }

    [HttpPost]
    // [ValidateAntiForgeryToken]
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

        var result = await _aiService.RefineTeacherNoteAsync(rawNote);

        if (!result.Success)
        {
            return Ok(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, suggestion = result.Data?.Suggestion });
    }

    [HttpPost]
    // [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeVideo([FromBody] AnalyzeVideoRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            return Ok(new { success = false, message = "Đường dẫn video không hợp lệ. Vui lòng tải video lên trước." });
        }

        var filePath = Path.Combine(_env.WebRootPath, request.VideoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(filePath))
        {
            return Ok(new { success = false, message = "Không tìm thấy file video trên máy chủ nội bộ." });
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var mimeType = ext == ".mov" ? "video/quicktime" : (ext == ".avi" ? "video/x-msvideo" : "video/mp4");

        var result = await _aiService.AnalyzePresentationVideoAsync(filePath, mimeType);

        if (!result.Success)
        {
            return Ok(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, result = result.Data });
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
