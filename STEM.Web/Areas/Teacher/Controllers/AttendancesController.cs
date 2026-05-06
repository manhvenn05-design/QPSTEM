using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class AttendancesController : Controller
{
    private readonly ApplicationDbContext _context;

    public AttendancesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "today", string? q = null)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var filters = new[]
        {
            new TeacherAttendanceFilterViewModel { Key = "today", Label = "Hôm nay" },
            new TeacherAttendanceFilterViewModel { Key = "open", Label = "Chưa đủ" },
            new TeacherAttendanceFilterViewModel { Key = "upcoming", Label = "Sắp tới" },
            new TeacherAttendanceFilterViewModel { Key = "completed", Label = "Đã đủ" },
            new TeacherAttendanceFilterViewModel { Key = "all", Label = "Tất cả" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "today" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "today";
        }

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId.Value)
            .Select(x => new SessionProjection
            {
                SessionId = x.Id,
                SessionNo = x.SessionNo,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                Topic = x.Topic,
                Date = x.Date,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                (x.Topic != null && x.Topic.Contains(searchTerm)));
        }

        query = ApplyFilter(query, normalizedFilter, today);

        var sessions = await query
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .ToListAsync();

        ViewData["Title"] = "Điểm danh";

        return View(new TeacherAttendanceIndexViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalSessions = sessions.Count,
            Filters = filters,
            Sessions = sessions.Select(x => MapSessionItem(x, today)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Board(int sessionId)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var model = await BuildBoardViewModelAsync(sessionId, teacherId.Value);
        if (model == null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Điểm danh";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Board(TeacherAttendanceBoardViewModel model)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var session = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == model.SessionId && x.Class.TeacherId == teacherId.Value)
            .Select(x => new { x.Id, x.ClassId })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return NotFound();
        }

        var validStudentIds = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.ClassId == session.ClassId)
            .Select(x => x.StudentId)
            .ToListAsync();

        var validIdSet = validStudentIds.ToHashSet();
        var submittedRows = model.Rows.Where(x => validIdSet.Contains(x.StudentId)).ToList();

        if (submittedRows.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Lớp này chưa có học viên để điểm danh.");
        }

        var existingAttendances = await _context.Attendances
            .Where(x => x.SessionId == model.SessionId && validIdSet.Contains(x.StudentId))
            .ToListAsync();

        foreach (var row in submittedRows)
        {
            var attendance = existingAttendances.FirstOrDefault(x => x.StudentId == row.StudentId);
            if (attendance == null)
            {
                _context.Attendances.Add(new Attendance
                {
                    SessionId = model.SessionId,
                    StudentId = row.StudentId,
                    IsPresent = row.IsPresent,
                    TeacherRawNote = NormalizeText(row.TeacherRawNote),
                    ProductMediaUrls = NormalizeText(row.ProductMediaUrls)
                });
            }
            else
            {
                attendance.IsPresent = row.IsPresent;
                attendance.TeacherRawNote = NormalizeText(row.TeacherRawNote);
                attendance.ProductMediaUrls = NormalizeText(row.ProductMediaUrls);
            }
        }

        if (!ModelState.IsValid)
        {
            var fallbackModel = await BuildBoardViewModelAsync(model.SessionId, teacherId.Value, submittedRows);
            if (fallbackModel == null)
            {
                return NotFound();
            }

            ViewData["Title"] = "Điểm danh";
            return View(fallbackModel);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã lưu điểm danh cho buổi học.";
        return RedirectToAction(nameof(Board), new { sessionId = model.SessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllPresent(int sessionId)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var session = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId && x.Class.TeacherId == teacherId.Value)
            .Select(x => new
            {
                x.Id,
                StudentIds = x.Class.Enrollments.Select(e => e.StudentId).ToList()
            })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return NotFound();
        }

        if (session.StudentIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Lớp này chưa có học viên để điểm danh.";
            return RedirectToAction(nameof(Board), new { sessionId });
        }

        var existingAttendances = await _context.Attendances
            .Where(x => x.SessionId == sessionId && session.StudentIds.Contains(x.StudentId))
            .ToListAsync();

        foreach (var studentId in session.StudentIds)
        {
            var attendance = existingAttendances.FirstOrDefault(x => x.StudentId == studentId);
            if (attendance == null)
            {
                _context.Attendances.Add(new Attendance
                {
                    SessionId = sessionId,
                    StudentId = studentId,
                    IsPresent = true
                });
                continue;
            }

            attendance.IsPresent = true;
            attendance.TeacherRawNote ??= null;
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã đánh dấu nhanh cả lớp có mặt.";
        return RedirectToAction(nameof(Board), new { sessionId });
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }

    private async Task<TeacherAttendanceBoardViewModel?> BuildBoardViewModelAsync(
        int sessionId,
        int teacherId,
        IReadOnlyCollection<TeacherAttendanceBoardRowViewModel>? overrides = null)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId && x.Class.TeacherId == teacherId)
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.Topic,
                x.TeachingMaterialUrl,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.ClassId,
                x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                PresentCount = x.Attendances.Count(a => a.IsPresent),
                AbsentCount = x.Attendances.Count(a => !a.IsPresent)
            })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return null;
        }

        var studentRows = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.ClassId == session.ClassId)
            .OrderBy(x => x.Student.FullName)
            .Select(x => new TeacherAttendanceBoardRowViewModel
            {
                StudentId = x.StudentId,
                StudentName = x.Student.FullName,
                StudentUsername = x.Student.Username,
                StudentAvatarUrl = x.Student.AvatarUrl,
                AttendanceId = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => (int?)a.Id)
                    .FirstOrDefault(),
                IsPresent = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => (bool?)a.IsPresent)
                    .FirstOrDefault() ?? true,
                TeacherRawNote = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => a.TeacherRawNote)
                    .FirstOrDefault(),
                ProductMediaUrls = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => a.ProductMediaUrls)
                    .FirstOrDefault()
            })
            .ToListAsync();

        if (overrides != null)
        {
            var overrideMap = overrides.ToDictionary(x => x.StudentId);
            foreach (var row in studentRows)
            {
                if (!overrideMap.TryGetValue(row.StudentId, out var overrideRow))
                {
                    continue;
                }

                row.AttendanceId = overrideRow.AttendanceId;
                row.IsPresent = overrideRow.IsPresent;
                row.TeacherRawNote = overrideRow.TeacherRawNote;
                row.ProductMediaUrls = overrideRow.ProductMediaUrls;
            }
        }

        foreach (var row in studentRows)
        {
            row.MediaHint = string.IsNullOrWhiteSpace(row.ProductMediaUrls)
                ? "Dán link ảnh hoặc video nếu đã có."
                : "Có thể dán thêm link mới nếu cần.";
        }

        return new TeacherAttendanceBoardViewModel
        {
            SessionId = session.Id,
            SessionLabel = $"Buổi số {session.SessionNo:00}",
            ClassCode = session.ClassCode,
            CourseName = session.CourseName,
            Topic = string.IsNullOrWhiteSpace(session.Topic) ? "Chưa có chủ đề" : session.Topic.Trim(),
            ScheduleText = $"{session.Date:dd/MM/yyyy} · {session.StartTime:HH\\:mm} - {session.EndTime:HH\\:mm}",
            TeachingMaterialUrl = session.TeachingMaterialUrl ?? string.Empty,
            StudentCount = session.StudentCount,
            AttendanceCount = session.AttendanceCount,
            PresentCount = studentRows.Count(x => x.IsPresent),
            AbsentCount = studentRows.Count(x => !x.IsPresent),
            Rows = studentRows
        };
    }

    private static IQueryable<SessionProjection> ApplyFilter(IQueryable<SessionProjection> query, string filter, DateOnly today)
    {
        return filter switch
        {
            "today" => query.Where(x => x.Date == today),
            "open" => query.Where(x => x.Date <= today && x.StudentCount > 0 && x.AttendanceCount < x.StudentCount),
            "upcoming" => query.Where(x => x.Date > today),
            "completed" => query.Where(x => x.StudentCount == 0 || x.AttendanceCount >= x.StudentCount),
            _ => query
        };
    }

    private static TeacherAttendanceSessionItemViewModel MapSessionItem(SessionProjection item, DateOnly today)
    {
        var statusLabel = item.Date > today
            ? "Sắp tới"
            : item.StudentCount == 0
                ? "Chưa có học viên"
                : item.AttendanceCount >= item.StudentCount
                    ? "Đã đủ"
                    : "Chưa đủ";

        var statusBadgeClass = item.Date > today
            ? "teacher-tag teacher-tag--warning"
            : item.StudentCount == 0
                ? "teacher-tag teacher-tag--neutral"
                : item.AttendanceCount >= item.StudentCount
                    ? "teacher-tag teacher-tag--success"
                    : "teacher-tag teacher-tag--neutral";

        return new TeacherAttendanceSessionItemViewModel
        {
            SessionId = item.SessionId,
            SessionLabel = $"Buổi số {item.SessionNo:00}",
            ClassCode = item.ClassCode,
            CourseName = item.CourseName,
            Topic = string.IsNullOrWhiteSpace(item.Topic) ? "Chưa có chủ đề" : item.Topic.Trim(),
            ScheduleText = $"{item.Date:dd/MM/yyyy} · {item.StartTime:HH\\:mm} - {item.EndTime:HH\\:mm}",
            StudentCount = item.StudentCount,
            AttendanceCount = item.AttendanceCount,
            CompletionText = item.StudentCount == 0
                ? "Lớp này chưa có học viên."
                : $"{item.AttendanceCount}/{item.StudentCount} học viên đã được ghi nhận.",
            StatusLabel = statusLabel,
            StatusBadgeClass = statusBadgeClass
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class SessionProjection
    {
        public int SessionId { get; set; }
        public int SessionNo { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string? Topic { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int StudentCount { get; set; }
        public int AttendanceCount { get; set; }
    }
}
