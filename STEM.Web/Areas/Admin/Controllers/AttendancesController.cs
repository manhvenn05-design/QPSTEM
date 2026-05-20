using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using STEM.Web.Services;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AttendancesController : Controller
{
    private const int PageSize = 10;
    private static readonly HashSet<string> AllowedAttendanceStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "present",
        "absent"
    };

    private readonly ApplicationDbContext _context;

    public AttendancesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null, int page = 1)
    {
        var filters = new[]
        {
            new AttendanceFilterViewModel { Key = "all", Label = "Tất cả" },
            new AttendanceFilterViewModel { Key = "today", Label = "Hôm nay" },
            new AttendanceFilterViewModel { Key = "upcoming", Label = "Sắp tới" },
            new AttendanceFilterViewModel { Key = "past", Label = "Đã diễn ra" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Sessions
            .AsNoTracking()
            .Select(x => new SessionAttendanceProjection
            {
                SessionId = x.Id,
                SessionNo = x.SessionNo,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.Class.Teacher.FullName,
                Date = x.Date,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                PresentCount = x.Attendances.Count(a => a.IsPresent),
                AbsentCount = x.Attendances.Count(a => !a.IsPresent)
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                x.TeacherName.Contains(searchTerm));
        }

        query = ApplyFilter(query, normalizedFilter, today);

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var sessions = await query
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var model = new AttendanceManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalSessions = totalItems,
            CurrentPage = page,
            TotalPages = totalPages,
            Filters = filters,
            Sessions = sessions.Select(x => MapSessionItem(x, today)).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Board(int sessionId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var sessionInfo = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.ClassId,
                x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.Class.Teacher.FullName,
                x.Date,
                x.StartTime,
                x.EndTime,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                PresentCount = x.Attendances.Count(a => a.IsPresent),
                AbsentCount = x.Attendances.Count(a => !a.IsPresent)
            })
            .FirstOrDefaultAsync();

        if (sessionInfo == null)
        {
            return NotFound();
        }

        var students = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.ClassId == sessionInfo.ClassId)
            .OrderBy(x => x.Student.FullName)
            .Select(x => new
            {
                x.StudentId,
                x.Student.FullName,
                x.Student.Username,
                x.Student.AvatarUrl,
                Attendance = x.Student.Attendances
                    .Where(a => a.SessionId == sessionId)
                    .Select(a => new
                    {
                        a.Id,
                        a.IsPresent,
                        a.TeacherRawNote,
                        a.ProductMediaUrls
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var model = new AttendanceBoardViewModel
        {
            SessionId = sessionInfo.Id,
            SessionLabel = $"Buổi số {sessionInfo.SessionNo:00}",
            ClassCode = sessionInfo.ClassCode,
            CourseName = sessionInfo.CourseName,
            TeacherName = sessionInfo.TeacherName,
            ScheduleText = $"{sessionInfo.Date:dd/MM/yyyy} · {sessionInfo.StartTime:HH\\:mm} - {sessionInfo.EndTime:HH\\:mm}",
            StudentCount = sessionInfo.StudentCount,
            AttendanceCount = sessionInfo.AttendanceCount,
            PresentCount = sessionInfo.PresentCount,
            AbsentCount = sessionInfo.AbsentCount,
            MissingCount = Math.Max(0, sessionInfo.StudentCount - sessionInfo.AttendanceCount),
            CompletionPercent = sessionInfo.StudentCount == 0
                ? 0
                : (int)Math.Round((double)sessionInfo.AttendanceCount * 100 / sessionInfo.StudentCount),
            StatusLabel = sessionInfo.Date > today ? "Sắp tới" : sessionInfo.Date < today ? "Đã diễn ra" : "Hôm nay",
            StatusBadgeClass = sessionInfo.Date > today
                ? "bg-[#fff4e8] text-[#9b682f]"
                : sessionInfo.Date < today
                    ? "bg-[#eeeee9] text-[#42493d]"
                    : "bg-[#edf7e8] text-[#456c3f]",
            Rows = students.Select(x => new AttendanceBoardStudentRowViewModel
            {
                SessionId = sessionInfo.Id,
                StudentId = x.StudentId,
                StudentName = x.FullName,
                StudentUsername = x.Username,
                AvatarUrl = x.AvatarUrl,
                HasAttendance = x.Attendance != null,
                AttendanceId = x.Attendance?.Id,
                IsPresent = x.Attendance?.IsPresent ?? false,
                AttendanceStatus = MapAttendanceStatus(x.Attendance?.IsPresent, x.Attendance?.TeacherRawNote),
                PresenceLabel = x.Attendance == null ? "Chưa ghi" : x.Attendance.IsPresent ? "Có mặt" : "Vắng",
                PresenceBadgeClass = x.Attendance == null
                    ? "bg-[#eeeee9] text-[#42493d]"
                    : x.Attendance.IsPresent
                        ? "bg-[#edf7e8] text-[#456c3f]"
                        : "bg-[#ffdad6] text-[#ba1a1a]",
                NotePreview = BuildPreview(x.Attendance?.TeacherRawNote, "Chưa có ghi chú."),
                MediaSummary = string.IsNullOrWhiteSpace(x.Attendance?.ProductMediaUrls) ? "Chưa có media" : "Đã có media",
                TeacherRawNote = x.Attendance?.TeacherRawNote,
                ProductMediaUrls = x.Attendance?.ProductMediaUrls
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickSave(AttendanceQuickUpdateViewModel model)
    {
        var sessionExists = await _context.Sessions.AnyAsync(x => x.Id == model.SessionId);
        if (!sessionExists)
        {
            TempData["ErrorMessage"] = "Buổi học không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        model.AttendanceStatus = string.IsNullOrWhiteSpace(model.AttendanceStatus)
            ? "pending"
            : model.AttendanceStatus.Trim().ToLowerInvariant();

        if (!AllowedAttendanceStatuses.Contains(model.AttendanceStatus) || model.AttendanceStatus == "pending")
        {
            TempData["ErrorMessage"] = "Vui lòng chọn trạng thái điểm danh trước khi lưu.";
            return RedirectToAction(nameof(Board), new { sessionId = model.SessionId });
        }

        var isStudentInSessionClass = await _context.Enrollments.AnyAsync(x =>
            x.StudentId == model.StudentId &&
            x.Class.Sessions.Any(s => s.Id == model.SessionId));

        if (!isStudentInSessionClass)
        {
            TempData["ErrorMessage"] = "Học viên không thuộc lớp của buổi học này.";
            return RedirectToAction(nameof(Board), new { sessionId = model.SessionId });
        }

        var entity = await _context.Attendances
            .FirstOrDefaultAsync(x => x.SessionId == model.SessionId && x.StudentId == model.StudentId);

        if (entity == null)
        {
            entity = new Attendance
            {
                SessionId = model.SessionId,
                StudentId = model.StudentId
            };
            _context.Attendances.Add(entity);
        }

        entity.IsPresent = model.AttendanceStatus == "present";
        entity.TeacherRawNote = NormalizeText(model.TeacherRawNote);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã lưu điểm danh.";
        return RedirectToAction(nameof(Board), new { sessionId = model.SessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllPresent(int sessionId)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id,
                StudentIds = x.Class.Enrollments.Select(e => e.StudentId).ToList()
            })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            TempData["ErrorMessage"] = "Buổi học không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (session.StudentIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Lớp này chưa có học viên để điểm danh.";
            return RedirectToAction(nameof(Board), new { sessionId });
        }

        var existingStudentIds = await _context.Attendances
            .Where(x => x.SessionId == sessionId)
            .Select(x => x.StudentId)
            .ToListAsync();

        var missingStudentIds = session.StudentIds
            .Except(existingStudentIds)
            .ToList();

        if (missingStudentIds.Count == 0)
        {
            TempData["SuccessMessage"] = "Tất cả học viên đã có bản ghi điểm danh.";
            return RedirectToAction(nameof(Board), new { sessionId });
        }

        foreach (var studentId in missingStudentIds)
        {
            _context.Attendances.Add(new Attendance
            {
                SessionId = sessionId,
                StudentId = studentId,
                IsPresent = true
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã đánh dấu nhanh có mặt cho {missingStudentIds.Count} học viên.";
        return RedirectToAction(nameof(Board), new { sessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? sessionId = null, int? studentId = null)
    {
        var model = new CreateAttendanceViewModel
        {
            SessionId = sessionId,
            StudentId = studentId
        };

        await PopulateOptionsAsync(model, sessionId, studentId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAttendanceViewModel model)
    {
        await PopulateOptionsAsync(model, model.SessionId, model.StudentId);
        await ValidateAttendanceAsync(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Attendance
        {
            SessionId = model.SessionId!.Value,
            StudentId = model.StudentId!.Value,
            IsPresent = model.IsPresent,
            TeacherRawNote = NormalizeText(model.TeacherRawNote),
            AiEvaluation = NormalizeText(model.AiEvaluation),
            VideoTranscript = NormalizeText(model.VideoTranscript)
        };

        _context.Attendances.Add(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã lưu điểm danh cho học viên.";
        return RedirectToAction(nameof(Board), new { sessionId = entity.SessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _context.Attendances
            .Include(x => x.SkillScores)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditAttendanceViewModel
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            StudentId = entity.StudentId,
            IsPresent = entity.IsPresent,
            ProductMediaUrls = entity.ProductMediaUrls,
            TeacherRawNote = entity.TeacherRawNote,
            AiEvaluation = entity.AiEvaluation,
            VideoTranscript = entity.VideoTranscript,
            SkillScores = entity.SkillScores.Select(s => new AttendanceSkillScoreItemViewModel
            {
                Id = s.Id,
                SkillName = s.SkillName,
                Score = s.Score,
                Feedback = s.Feedback
            }).ToList()
        };

        await PopulateOptionsAsync(model, model.SessionId, model.StudentId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditAttendanceViewModel model)
    {
        await PopulateOptionsAsync(model, model.SessionId, model.StudentId);

        var entity = await _context.Attendances.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        await ValidateAttendanceAsync(model, model.Id);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entity.SessionId = model.SessionId!.Value;
        entity.StudentId = model.StudentId!.Value;
        entity.IsPresent = model.IsPresent;
        entity.TeacherRawNote = NormalizeText(model.TeacherRawNote);
        entity.AiEvaluation = NormalizeText(model.AiEvaluation);
        entity.VideoTranscript = NormalizeText(model.VideoTranscript);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã cập nhật điểm danh.";
        return RedirectToAction(nameof(Board), new { sessionId = entity.SessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var model = await _context.Attendances
            .Include(x => x.SkillScores)
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AttendanceDetailsViewModel
            {
                Id = x.Id,
                SessionId = x.SessionId,
                StudentName = x.Student.FullName,
                StudentUsername = x.Student.Username,
                StudentAvatarUrl = x.Student.AvatarUrl,
                SessionLabel = $"Buổi số {x.Session.SessionNo:00}",
                ClassCode = x.Session.Class.ClassCode,
                CourseName = x.Session.Class.Course.Name,
                TeacherName = x.Session.Class.Teacher.FullName,
                SessionDate = x.Session.Date,
                StartTime = x.Session.StartTime,
                EndTime = x.Session.EndTime,
                IsPresent = x.IsPresent,
                PresenceLabel = x.IsPresent ? "Có mặt" : "Vắng",
                PresenceBadgeClass = x.IsPresent ? "bg-[#edf7e8] text-[#456c3f]" : "bg-[#ffdad6] text-[#ba1a1a]",
                ProductMediaUrls = x.ProductMediaUrls,
                TeacherRawNote = x.TeacherRawNote,
                AiEvaluation = AiEvaluationFormatter.FormatForDisplay(x.AiEvaluation),
                VideoTranscript = x.VideoTranscript,
                SkillScores = x.SkillScores.Select(s => new AttendanceSkillScoreItemViewModel
                {
                    Id = s.Id,
                    SkillName = s.SkillName,
                    Score = s.Score,
                    Feedback = s.Feedback
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Attendances.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bản ghi điểm danh cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        var sessionId = entity.SessionId;
        _context.Attendances.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa bản ghi điểm danh.";
        return RedirectToAction(nameof(Board), new { sessionId });
    }

    [HttpGet]
    public async Task<IActionResult> StudentsForSession(int? sessionId)
    {
        if (!sessionId.HasValue)
        {
            return Json(Array.Empty<object>());
        }

        var students = await GetStudentOptionsAsync(sessionId.Value);

        return Json(students.Select(x => new
        {
            value = x.Value,
            text = x.Text
        }));
    }

    private async Task PopulateOptionsAsync(CreateAttendanceViewModel model, int? sessionId, int? studentId)
    {
        model.SessionOptions = await _context.Sessions
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.Class.ClassCode)
            .ThenBy(x => x.SessionNo)
            .Select(x => new SelectListItem(
                $"{x.Class.ClassCode} - Buổi số {x.SessionNo:00} - {x.Date:dd/MM/yyyy}",
                x.Id.ToString()))
            .ToListAsync();

        model.StudentOptions = sessionId.HasValue
            ? await GetStudentOptionsAsync(sessionId.Value)
            : [];

        if (sessionId.HasValue)
        {
            model.SessionSummary = await _context.Sessions
                .AsNoTracking()
                .Where(x => x.Id == sessionId.Value)
                .Select(x => $"{x.Class.ClassCode} · Buổi số {x.SessionNo:00} · {x.Date:dd/MM/yyyy}")
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        if (studentId.HasValue)
        {
            model.StudentSummary = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == studentId.Value)
                .Select(x => $"{x.FullName} ({x.Username})")
                .FirstOrDefaultAsync() ?? string.Empty;
        }
    }

    private async Task<IReadOnlyList<SelectListItem>> GetStudentOptionsAsync(int sessionId)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.Class.Sessions.Any(s => s.Id == sessionId))
            .OrderBy(x => x.Student.FullName)
            .Select(x => new SelectListItem(
                $"{x.Student.FullName} ({x.Student.Username})",
                x.StudentId.ToString()))
            .Distinct()
            .ToListAsync();
    }

    private async Task ValidateAttendanceAsync(CreateAttendanceViewModel model, int? currentId = null)
    {
        if (!model.SessionId.HasValue || !await _context.Sessions.AnyAsync(x => x.Id == model.SessionId.Value))
        {
            ModelState.AddModelError(nameof(model.SessionId), "Buổi học không hợp lệ.");
            return;
        }

        if (!model.StudentId.HasValue || !await _context.Users.AnyAsync(x => x.Id == model.StudentId.Value))
        {
            ModelState.AddModelError(nameof(model.StudentId), "Học viên không hợp lệ.");
            return;
        }

        var isStudentInSessionClass = await _context.Enrollments.AnyAsync(x =>
            x.StudentId == model.StudentId.Value &&
            x.Class.Sessions.Any(s => s.Id == model.SessionId.Value));

        if (!isStudentInSessionClass)
        {
            ModelState.AddModelError(nameof(model.StudentId), "Học viên không thuộc lớp của buổi học đã chọn.");
        }

        var duplicateAttendance = await _context.Attendances.AnyAsync(x =>
            x.SessionId == model.SessionId.Value &&
            x.StudentId == model.StudentId.Value &&
            (!currentId.HasValue || x.Id != currentId.Value));

        if (duplicateAttendance)
        {
            ModelState.AddModelError(nameof(model.StudentId), "Học viên này đã có bản ghi điểm danh trong buổi học đã chọn.");
        }
    }

    private static IQueryable<SessionAttendanceProjection> ApplyFilter(IQueryable<SessionAttendanceProjection> query, string filter, DateOnly today)
    {
        return filter switch
        {
            "today" => query.Where(x => x.Date == today),
            "upcoming" => query.Where(x => x.Date > today),
            "past" => query.Where(x => x.Date < today),
            _ => query
        };
    }

    private static AttendanceSessionItemViewModel MapSessionItem(SessionAttendanceProjection item, DateOnly today)
    {
        var statusLabel = item.Date > today ? "Sắp tới" : item.Date < today ? "Đã diễn ra" : "Hôm nay";
        var statusBadgeClass = item.Date > today
            ? "bg-[#fff4e8] text-[#9b682f]"
            : item.Date < today
                ? "bg-[#eeeee9] text-[#42493d]"
                : "bg-[#edf7e8] text-[#456c3f]";

        return new AttendanceSessionItemViewModel
        {
            SessionId = item.SessionId,
            SessionLabel = $"Buổi số {item.SessionNo:00}",
            ClassCode = item.ClassCode,
            CourseName = item.CourseName,
            TeacherName = item.TeacherName,
            ScheduleText = $"{item.Date:dd/MM/yyyy} · {item.StartTime:HH\\:mm} - {item.EndTime:HH\\:mm}",
            StudentCount = item.StudentCount,
            AttendanceCount = item.AttendanceCount,
            PresentCount = item.PresentCount,
            AbsentCount = item.AbsentCount,
            CompletionText = item.StudentCount == 0
                ? "Chưa có học viên"
                : $"{item.AttendanceCount}/{item.StudentCount} học viên đã ghi nhận",
            StatusLabel = statusLabel,
            StatusBadgeClass = statusBadgeClass
        };
    }

    private static string BuildPreview(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return normalized.Length <= 80 ? normalized : $"{normalized[..77]}...";
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string MapAttendanceStatus(bool? isPresent, string? teacherRawNote)
    {
        if (!isPresent.HasValue)
        {
            return "pending";
        }

        if (isPresent.Value)
        {
            return "present";
        }

        return "absent";
    }

    private sealed class SessionAttendanceProjection
    {
        public int SessionId { get; set; }
        public int SessionNo { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int StudentCount { get; set; }
        public int AttendanceCount { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
    }
}

