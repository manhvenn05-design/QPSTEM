using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class AICopilotController : Controller
{
    private readonly ApplicationDbContext _context;

    public AICopilotController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "ready", int? attendanceId = null)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var filters = new[]
        {
            new TeacherAIFilterViewModel { Key = "ready", Label = "Sẵn sàng" },
            new TeacherAIFilterViewModel { Key = "completed", Label = "Đã có nhận xét" },
            new TeacherAIFilterViewModel { Key = "missing-note", Label = "Thiếu ghi chú" },
            new TeacherAIFilterViewModel { Key = "all", Label = "Tất cả" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "ready" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "ready";
        }

        var rows = await _context.Attendances
            .AsNoTracking()
            .Where(x => x.Session.Class.TeacherId == teacherId.Value)
            .Select(x => new
            {
                x.Id,
                StudentName = x.Student.FullName,
                ClassCode = x.Session.Class.ClassCode,
                x.Session.SessionNo,
                x.Session.Topic,
                x.TeacherRawNote,
                x.ProductMediaUrls,
                x.AiEvaluation
            })
            .ToListAsync();

        var candidates = rows
            .Where(x => normalizedFilter switch
            {
                "ready" => !string.IsNullOrWhiteSpace(x.TeacherRawNote) && !string.IsNullOrWhiteSpace(x.ProductMediaUrls) && string.IsNullOrWhiteSpace(x.AiEvaluation),
                "completed" => !string.IsNullOrWhiteSpace(x.AiEvaluation),
                "missing-note" => string.IsNullOrWhiteSpace(x.TeacherRawNote),
                _ => true
            })
            .OrderBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .ThenBy(x => x.StudentName)
            .Select(x =>
            {
                var hasNote = !string.IsNullOrWhiteSpace(x.TeacherRawNote);
                var hasMedia = !string.IsNullOrWhiteSpace(x.ProductMediaUrls);
                var hasEvaluation = !string.IsNullOrWhiteSpace(x.AiEvaluation);

                var (statusLabel, statusBadgeClass) = hasEvaluation
                    ? ("Đã có nhận xét", "teacher-tag teacher-tag--success")
                    : hasNote && hasMedia
                        ? ("Sẵn sàng", "teacher-tag teacher-tag--warning")
                        : hasNote
                            ? ("Thiếu media", "teacher-tag teacher-tag--neutral")
                            : ("Thiếu ghi chú", "teacher-tag teacher-tag--neutral");

                return new TeacherAICandidateItemViewModel
                {
                    AttendanceId = x.Id,
                    StudentName = x.StudentName,
                    ClassCode = x.ClassCode,
                    SessionLabel = $"Buổi số {x.SessionNo:00}",
                    StatusLabel = statusLabel,
                    StatusBadgeClass = statusBadgeClass
                };
            })
            .ToList();

        var selectedId = attendanceId ?? candidates.FirstOrDefault()?.AttendanceId;
        TeacherAIPreviewViewModel? preview = null;

        if (selectedId.HasValue)
        {
            preview = await _context.Attendances
                .AsNoTracking()
                .Where(x => x.Id == selectedId.Value && x.Session.Class.TeacherId == teacherId.Value)
                .Select(x => new TeacherAIPreviewViewModel
                {
                    AttendanceId = x.Id,
                    StudentName = x.Student.FullName,
                    ClassCode = x.Session.Class.ClassCode,
                    SessionLabel = $"Buổi số {x.Session.SessionNo:00}",
                    Topic = string.IsNullOrWhiteSpace(x.Session.Topic) ? "Chưa cập nhật chủ đề" : x.Session.Topic,
                    RawNote = x.TeacherRawNote ?? string.Empty,
                    ExistingEvaluation = x.AiEvaluation ?? string.Empty
                })
                .FirstOrDefaultAsync();
        }

        var model = new TeacherAICopilotViewModel
        {
            SelectedFilter = normalizedFilter,
            SelectedAttendanceId = selectedId,
            ReadyCount = rows.Count(x => !string.IsNullOrWhiteSpace(x.TeacherRawNote) && !string.IsNullOrWhiteSpace(x.ProductMediaUrls) && string.IsNullOrWhiteSpace(x.AiEvaluation)),
            CompletedCount = rows.Count(x => !string.IsNullOrWhiteSpace(x.AiEvaluation)),
            MissingNoteCount = rows.Count(x => string.IsNullOrWhiteSpace(x.TeacherRawNote)),
            Filters = filters,
            Candidates = candidates,
            Preview = preview
        };

        ViewData["Title"] = "AI Copilot";
        return View(model);
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }
}
