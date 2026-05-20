using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Services;
using STEM.Web.Services.AI;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class AICopilotController : Controller
{
    private readonly IAIService _aiService;
    private readonly ILogger<AICopilotController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApplicationDbContext _context;
    private readonly AttendanceWorkflowService _attendanceWorkflow;

    private static readonly Dictionary<string, string> MimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".mp4",  "video/mp4" },
        { ".m4v",  "video/mp4" },
        { ".mov",  "video/quicktime" },
        { ".avi",  "video/x-msvideo" },
        { ".webm", "video/webm" },
        { ".mkv",  "video/x-matroska" }
    };

    public AICopilotController(
        IAIService aiService,
        ILogger<AICopilotController> logger,
        IWebHostEnvironment env,
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext context,
        AttendanceWorkflowService attendanceWorkflow)
    {
        _aiService = aiService;
        _logger = logger;
        _env = env;
        _httpClientFactory = httpClientFactory;
        _context = context;
        _attendanceWorkflow = attendanceWorkflow;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefineNote(
        [FromBody] RefineNoteRequest? request,
        CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RawNote))
        {
            return Ok(new { success = false, message = "Ghi chú không được để trống." });
        }

        var rawNote = request.RawNote.Trim();
        if (rawNote.Length < 10)
        {
            return Ok(new { success = false, message = "Ghi chú quá ngắn. Vui lòng nhập ít nhất 10 ký tự để AI có cơ sở viết nhận xét." });
        }

        if (!IsMeaningfulTeacherNote(rawNote))
        {
            return Ok(new
            {
                success = false,
                message = "Ghi chú chưa đủ rõ nghĩa. Vui lòng nhập nhận xét cụ thể về tiến độ, thái độ hoặc phần học sinh làm được/chưa làm được."
            });
        }

        var result = await _aiService.RefineTeacherNoteAsync(rawNote, ct);

        return result.Success
            ? Ok(new { success = true, suggestion = result.Data?.Suggestion })
            : Ok(new { success = false, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnalyzeVideo(
        [FromBody] AnalyzeVideoRequest? request,
        CancellationToken ct)
    {
        if (request == null || request.AttendanceId <= 0 || string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            return Ok(new { success = false, message = "Dữ liệu phân tích video không hợp lệ." });
        }

        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var attendance = await _context.Attendances
            .Include(x => x.Session)
            .ThenInclude(x => x.Class)
            .FirstOrDefaultAsync(
                x => x.Id == request.AttendanceId &&
                     x.Session.Class.TeacherId == teacherId.Value,
                ct);

        if (attendance == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy bản ghi điểm danh cần phân tích." });
        }

        var editLockMessage = await _attendanceWorkflow.GetTeacherEditLockMessageAsync(
            teacherId.Value,
            attendance.Session.Date,
            attendance.Session.StartTime,
            attendance.Session.EndTime,
            ct);
        if (!string.IsNullOrWhiteSpace(editLockMessage))
        {
            return BadRequest(new { success = false, message = editLockMessage });
        }

        var normalizedVideoUrl = request.VideoUrl.Trim();
        if (!_attendanceWorkflow.IsValidProductMediaUrl(normalizedVideoUrl))
        {
            return Ok(new { success = false, message = "Đường dẫn video không hợp lệ hoặc không thuộc hệ thống lưu trữ." });
        }

        string? tempFilePath = null;
        try
        {
            string filePath;
            if (Uri.TryCreate(normalizedVideoUrl, UriKind.Absolute, out var absoluteUri) &&
                (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                tempFilePath = await DownloadVideoToTempFileAsync(absoluteUri, ct);
                filePath = tempFilePath;
            }
            else
            {
                var relativePath = normalizedVideoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                filePath = Path.Combine(_env.WebRootPath, relativePath);
            }

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Khong tim thay file video. Url: {VideoUrl}, FilePath: {FilePath}", normalizedVideoUrl, filePath);
                return Ok(new { success = false, message = "Không tìm thấy file video trên máy chủ." });
            }

            var ext = Path.GetExtension(filePath);
            if (!MimeMap.TryGetValue(ext, out var mimeType))
            {
                return Ok(new { success = false, message = $"Định dạng video '{ext}' không được hỗ trợ." });
            }

            var result = await _aiService.AnalyzePresentationVideoAsync(filePath, mimeType, ct);
            if (!result.Success || result.Data == null)
            {
                attendance.AiEvaluation = null;
                attendance.VideoTranscript = null;
                attendance.AiProcessStatus = AttendanceIntegrityRules.AiProcessStatusFailed;
                await _context.SaveChangesAsync(ct);
                await _attendanceWorkflow.RecomputeSessionPayrollStatusAsync(attendance.SessionId, ct);
                return Ok(new { success = false, message = result.Message });
            }

            attendance.ProductMediaUrls = normalizedVideoUrl;
            attendance.AiEvaluation = AttendanceIntegrityRules.SerializeAiResult(result.Data);
            attendance.AiProcessStatus = AttendanceIntegrityRules.AiProcessStatusCompleted;

            await _context.SaveChangesAsync(ct);
            await _attendanceWorkflow.RecomputeSessionPayrollStatusAsync(attendance.SessionId, ct);

            return Ok(new
            {
                success = true,
                attendanceId = attendance.Id,
                result = result.Data
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Khong the tai video tu cloud de phan tich.");
            return Ok(new { success = false, message = "Không thể tải video từ cloud về máy chủ để phân tích." });
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempFilePath) && System.IO.File.Exists(tempFilePath))
            {
                try
                {
                    System.IO.File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Khong the xoa file tam sau khi phan tich video: {TempFilePath}", tempFilePath);
                }
            }
        }
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }

    private async Task<string> DownloadVideoToTempFileAsync(Uri videoUri, CancellationToken ct)
    {
        var ext = Path.GetExtension(videoUri.AbsolutePath);
        if (!MimeMap.ContainsKey(ext))
        {
            throw new InvalidOperationException($"Dinh dang video '{ext}' khong duoc ho tro.");
        }

        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(videoUri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{ext}");

        await using (var sourceStream = await response.Content.ReadAsStreamAsync(ct))
        await using (var targetStream = System.IO.File.Create(tempFilePath))
        {
            await sourceStream.CopyToAsync(targetStream, ct);
        }

        _logger.LogInformation("Da tai video cloud ve file tam de phan tich: {VideoUri}", videoUri);
        return tempFilePath;
    }

    private static bool IsMeaningfulTeacherNote(string rawNote)
    {
        var normalized = rawNote.Trim();
        if (normalized.Length < 10)
        {
            return false;
        }

        var letterOrDigitChars = normalized.Where(char.IsLetterOrDigit).ToArray();
        if (letterOrDigitChars.Length < 8)
        {
            return false;
        }

        var distinctLetterOrDigitCount = letterOrDigitChars
            .Select(char.ToLowerInvariant)
            .Distinct()
            .Count();
        if (distinctLetterOrDigitCount < 4)
        {
            return false;
        }

        var words = Regex.Matches(normalized, @"[\p{L}\p{N}]+")
            .Select(x => x.Value)
            .Where(x => x.Length >= 2)
            .ToList();
        if (words.Count < 2)
        {
            return false;
        }

        var longestRepeatedRun = 1;
        var currentRun = 1;
        for (var i = 1; i < normalized.Length; i++)
        {
            if (char.ToLowerInvariant(normalized[i]) == char.ToLowerInvariant(normalized[i - 1]))
            {
                currentRun++;
                longestRepeatedRun = Math.Max(longestRepeatedRun, currentRun);
            }
            else
            {
                currentRun = 1;
            }
        }

        if (longestRepeatedRun >= 6)
        {
            return false;
        }

        var repeatedSameWordOnly = words.Count <= 3 &&
                                   words.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() == words.Count);

        return !repeatedSameWordOnly;
    }
}

public class RefineNoteRequest
{
    public string RawNote { get; set; } = string.Empty;
}

public class AnalyzeVideoRequest
{
    public int AttendanceId { get; set; }

    public string VideoUrl { get; set; } = string.Empty;
}
