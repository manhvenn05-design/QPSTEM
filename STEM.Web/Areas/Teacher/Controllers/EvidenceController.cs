using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class EvidenceController : Controller
{
    private readonly ApplicationDbContext _context;

    public EvidenceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "needs-media", string? q = null)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var filters = new[]
        {
            new TeacherEvidenceFilterViewModel { Key = "needs-media", Label = "Thiếu media" },
            new TeacherEvidenceFilterViewModel { Key = "needs-note", Label = "Thiếu ghi chú" },
            new TeacherEvidenceFilterViewModel { Key = "ready", Label = "Đã đủ" },
            new TeacherEvidenceFilterViewModel { Key = "all", Label = "Tất cả" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "needs-media" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "needs-media";
        }

        var searchTerm = q?.Trim() ?? string.Empty;

        var query = _context.Attendances
            .AsNoTracking()
            .Where(x => x.Session.Class.TeacherId == teacherId.Value)
            .Select(x => new
            {
                x.Id,
                x.SessionId,
                StudentName = x.Student.FullName,
                StudentUsername = x.Student.Username,
                ClassCode = x.Session.Class.ClassCode,
                CourseName = x.Session.Class.Course.Name,
                x.Session.SessionNo,
                x.Session.Date,
                x.Session.Topic,
                x.TeacherRawNote,
                x.ProductMediaUrls
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.StudentName.Contains(searchTerm) ||
                x.StudentUsername.Contains(searchTerm) ||
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                (x.Topic != null && x.Topic.Contains(searchTerm)));
        }

        query = normalizedFilter switch
        {
            "needs-media" => query.Where(x => string.IsNullOrWhiteSpace(x.ProductMediaUrls)),
            "needs-note" => query.Where(x => string.IsNullOrWhiteSpace(x.TeacherRawNote)),
            "ready" => query.Where(x => !string.IsNullOrWhiteSpace(x.TeacherRawNote) && !string.IsNullOrWhiteSpace(x.ProductMediaUrls)),
            _ => query
        };

        var rows = await query
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .ThenBy(x => x.StudentName)
            .ToListAsync();

        var items = rows.Select(x =>
        {
            var mediaCount = SplitUrls(x.ProductMediaUrls).Count;
            var hasNote = !string.IsNullOrWhiteSpace(x.TeacherRawNote);
            var hasMedia = mediaCount > 0;

            var (statusLabel, statusBadgeClass) = hasNote && hasMedia
                ? ("Đã đủ", "teacher-tag teacher-tag--success")
                : hasMedia
                    ? ("Thiếu ghi chú", "teacher-tag teacher-tag--warning")
                    : hasNote
                        ? ("Thiếu media", "teacher-tag teacher-tag--warning")
                        : ("Thiếu cả hai", "teacher-tag teacher-tag--neutral");

            return new TeacherEvidenceItemViewModel
            {
                AttendanceId = x.Id,
                SessionId = x.SessionId,
                StudentName = x.StudentName,
                StudentUsername = x.StudentUsername,
                ClassCode = x.ClassCode,
                CourseName = x.CourseName,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                DateText = x.Date.ToString("dd/MM/yyyy"),
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                NotePreview = BuildPreview(x.TeacherRawNote, "Chưa có ghi chú"),
                MediaCount = mediaCount,
                MediaStatusLabel = statusLabel,
                MediaStatusBadgeClass = statusBadgeClass
            };
        }).ToList();

        var model = new TeacherEvidenceIndexViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalRows = items.Count,
            MissingMediaCount = await _context.Attendances.CountAsync(x =>
                x.Session.Class.TeacherId == teacherId.Value &&
                string.IsNullOrWhiteSpace(x.ProductMediaUrls)),
            MissingNoteCount = await _context.Attendances.CountAsync(x =>
                x.Session.Class.TeacherId == teacherId.Value &&
                string.IsNullOrWhiteSpace(x.TeacherRawNote)),
            ReadyCount = await _context.Attendances.CountAsync(x =>
                x.Session.Class.TeacherId == teacherId.Value &&
                !string.IsNullOrWhiteSpace(x.TeacherRawNote) &&
                !string.IsNullOrWhiteSpace(x.ProductMediaUrls)),
            Filters = filters,
            Items = items
        };

        ViewData["Title"] = "Minh chứng";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var item = await _context.Attendances
            .AsNoTracking()
            .Where(x => x.Id == id && x.Session.Class.TeacherId == teacherId.Value)
            .Select(x => new
            {
                x.Id,
                x.SessionId,
                StudentName = x.Student.FullName,
                StudentUsername = x.Student.Username,
                ClassCode = x.Session.Class.ClassCode,
                CourseName = x.Session.Class.Course.Name,
                x.Session.SessionNo,
                x.Session.Date,
                x.Session.StartTime,
                x.Session.EndTime,
                x.TeacherRawNote,
                x.ProductMediaUrls
            })
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound();
        }

        var model = new TeacherEvidenceDetailsViewModel
        {
            AttendanceId = item.Id,
            SessionId = item.SessionId,
            StudentName = item.StudentName,
            StudentUsername = item.StudentUsername,
            ClassCode = item.ClassCode,
            CourseName = item.CourseName,
            SessionLabel = $"Buổi số {item.SessionNo:00}",
            ScheduleText = $"{item.Date:dd/MM/yyyy} · {item.StartTime:HH\\:mm} - {item.EndTime:HH\\:mm}",
            TeacherRawNote = item.TeacherRawNote,
            MediaUrls = SplitUrls(item.ProductMediaUrls)
        };

        ViewData["Title"] = "Minh chứng";
        return View(model);
    }

    private static List<string> SplitUrls(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private static string BuildPreview(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return normalized.Length <= 90 ? normalized : $"{normalized[..87]}...";
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }
}
