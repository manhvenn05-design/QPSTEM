using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using STEM.Web.Services;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class AttendancesController : Controller
{
    private const long MaxVideoUploadBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov",
        ".avi",
        ".m4v",
        ".webm"
    };

    private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",
        "video/quicktime",
        "video/x-msvideo",
        "video/webm"
    };

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly AttendanceWorkflowService _attendanceWorkflow;

    public AttendancesController(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        AttendanceWorkflowService attendanceWorkflow)
    {
        _context = context;
        _fileStorage = fileStorage;
        _attendanceWorkflow = attendanceWorkflow;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null)
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

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var baseQuery = _context.Sessions
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

        var overviewSessions = await baseQuery
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .ToListAsync();

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                (x.Topic != null && x.Topic.Contains(searchTerm)));
        }

        if (normalizedFilter != "all")
        {
            query = ApplyFilter(query, normalizedFilter, today);
        }

        var visibleSessions = await query
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .ToListAsync();

        var mappedOverviewSessions = overviewSessions.Select(x => MapSessionItem(x, today)).ToList();
        var mappedVisibleSessions = visibleSessions.Select(x => MapSessionItem(x, today)).ToList();

        var sectionSource = string.IsNullOrWhiteSpace(searchTerm) && normalizedFilter == "all"
            ? mappedOverviewSessions
            : mappedVisibleSessions;

        ViewData["Title"] = "Điểm danh";

        return View(new TeacherAttendanceIndexViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalSessions = mappedVisibleSessions.Count,
            TodayCount = mappedOverviewSessions.Count(x => x.IsToday),
            OpenCount = mappedOverviewSessions.Count(x => x.NeedsAttention),
            UpcomingCount = mappedOverviewSessions.Count(x => x.IsUpcoming),
            CompletedCount = mappedOverviewSessions.Count(x => x.IsCompleted),
            Filters = filters,
            Sessions = mappedVisibleSessions,
            PrioritySessions = sectionSource.Where(x => x.NeedsAttention).OrderBy(x => x.Date).ThenBy(x => x.SessionLabel).ToList(),
            UpcomingSessions = sectionSource.Where(x => x.IsUpcoming).OrderBy(x => x.Date).ThenBy(x => x.SessionLabel).ToList(),
            CompletedSessions = sectionSource.Where(x => x.IsCompleted).OrderByDescending(x => x.Date).ThenBy(x => x.SessionLabel).ToList(),
            EmptySessions = sectionSource.Where(x => !x.HasStudents).OrderBy(x => x.Date).ThenBy(x => x.SessionLabel).ToList()
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

        var editLockMessage = await _attendanceWorkflow.GetTeacherEditLockMessageAsync(teacherId.Value, sessionId);
        var model = await BuildBoardViewModelAsync(sessionId, teacherId.Value, editLockMessage: editLockMessage);
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
            .Select(x => new
            {
                x.Id,
                x.ClassId,
                x.Date,
                x.StartTime,
                x.EndTime
            })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return NotFound();
        }

        var editLockMessage = await _attendanceWorkflow.GetTeacherEditLockMessageAsync(
            teacherId.Value,
            session.Date,
            session.StartTime,
            session.EndTime);
        if (!string.IsNullOrWhiteSpace(editLockMessage))
        {
            return WriteDenied(model.SessionId, editLockMessage);
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

        for (var i = 0; i < submittedRows.Count; i++)
        {
            if (!_attendanceWorkflow.IsValidProductMediaCollection(submittedRows[i].ProductMediaUrls))
            {
                ModelState.AddModelError($"Rows[{i}].ProductMediaUrls", "Link video không hợp lệ hoặc không thuộc kho lưu trữ của hệ thống.");
            }
        }

        var existingAttendances = await _context.Attendances
            .Where(x => x.SessionId == model.SessionId && validIdSet.Contains(x.StudentId))
            .ToListAsync();

        foreach (var row in submittedRows)
        {
            var attendance = existingAttendances.FirstOrDefault(x => x.StudentId == row.StudentId);
            var normalizedTeacherNote = NormalizeText(row.TeacherRawNote);
            var normalizedMediaUrls = NormalizeText(row.ProductMediaUrls);

            if (!row.IsPresent)
            {
                normalizedTeacherNote = null;
                normalizedMediaUrls = null;
            }

            if (attendance == null)
            {
                var newAttendance = new Attendance
                {
                    SessionId = model.SessionId,
                    StudentId = row.StudentId,
                    IsPresent = row.IsPresent,
                    IsExcused = row.IsExcused,
                    TeacherRawNote = normalizedTeacherNote,
                    ProductMediaUrls = normalizedMediaUrls
                };

                _context.Attendances.Add(newAttendance);
                existingAttendances.Add(newAttendance);
                continue;
            attendance.IsPresent = row.IsPresent;
            attendance.IsExcused = row.IsExcused;
            attendance.TeacherRawNote = normalizedTeacherNote;

            if (!row.IsPresent)
            {
                attendance.ProductMediaUrls = null;
                attendance.AiEvaluation = null;
                attendance.VideoTranscript = null;
                attendance.AiProcessStatus = null;
            }
            else if (!string.Equals(attendance.ProductMediaUrls, normalizedMediaUrls, StringComparison.Ordinal))
            {
                attendance.ProductMediaUrls = normalizedMediaUrls;
                attendance.AiEvaluation = null;
                attendance.VideoTranscript = null;
                attendance.AiProcessStatus = null;
            }
        }

        if (!ModelState.IsValid)
        {
            var fallbackModel = await BuildBoardViewModelAsync(
                model.SessionId,
                teacherId.Value,
                submittedRows,
                editLockMessage);
            if (fallbackModel == null)
            {
                return NotFound();
            }

            if (IsAjaxRequest())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu điểm danh chưa hợp lệ.",
                    errors = ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .SelectMany(x => x.Value!.Errors.Select(error => new { field = x.Key, message = error.ErrorMessage }))
                        .ToList()
                });
            }

            ViewData["Title"] = "Điểm danh";
            return View(fallbackModel);
        }

        await _context.SaveChangesAsync();
        var payrollStatus = await _attendanceWorkflow.RecomputeSessionPayrollStatusAsync(model.SessionId);

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                payrollStatus,
                rows = existingAttendances
                    .Select(x => new { x.Id, x.StudentId })
                    .OrderBy(x => x.StudentId)
                    .ToList()
            });
        }

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
                x.Date,
                x.StartTime,
                x.EndTime,
                StudentIds = x.Class.Enrollments.Select(e => e.StudentId).ToList()
            })
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return NotFound();
        }

        var editLockMessage = await _attendanceWorkflow.GetTeacherEditLockMessageAsync(
            teacherId.Value,
            session.Date,
            session.StartTime,
            session.EndTime);
        if (!string.IsNullOrWhiteSpace(editLockMessage))
        {
            TempData["ErrorMessage"] = editLockMessage;
            return RedirectToAction(nameof(Board), new { sessionId });
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
            attendance.IsExcused = false;
        }

        await _context.SaveChangesAsync();
        await _attendanceWorkflow.RecomputeSessionPayrollStatusAsync(sessionId);

        TempData["SuccessMessage"] = "Đã đánh dấu nhanh cả lớp có mặt.";
        return RedirectToAction(nameof(Board), new { sessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxVideoUploadBytes)]
    public async Task<IActionResult> UploadVideo(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return Json(new { success = false, message = "Vui lòng chọn một file video hợp lệ." });
        }

        if (file.Length > MaxVideoUploadBytes)
        {
            return Json(new { success = false, message = "File video quá lớn. Vui lòng chọn file dưới 50MB." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedVideoExtensions.Contains(extension))
        {
            return Json(new { success = false, message = "Định dạng video không được hỗ trợ." });
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedVideoContentTypes.Contains(file.ContentType))
        {
            return Json(new { success = false, message = "Loại nội dung file không hợp lệ." });
        }

        try
        {
            var fileUrl = await _fileStorage.SaveFileAsync(file, "videos");
            return Json(new { success = true, url = fileUrl, fileName = file.FileName });
        }
        catch
        {
            return Json(new { success = false, message = "Không thể lưu video lên hệ thống." });
        }
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }

    private async Task<TeacherAttendanceBoardViewModel?> BuildBoardViewModelAsync(
        int sessionId,
        int teacherId,
        IReadOnlyCollection<TeacherAttendanceBoardRowViewModel>? overrides = null,
        string? editLockMessage = null)
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
                x.PayrollStatus,
                x.ClassId,
                x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                StudentCount = x.Class.Enrollments.Count
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
                HasAttendance = x.Student.Attendances.Any(a => a.SessionId == session.Id),
                IsPresent = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => (bool?)a.IsPresent)
                    .FirstOrDefault() ?? false,
                IsExcused = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => (bool?)a.IsExcused)
                    .FirstOrDefault() ?? false,
                TeacherRawNote = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => a.TeacherRawNote)
                    .FirstOrDefault(),
                ProductMediaUrls = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => a.ProductMediaUrls)
                    .FirstOrDefault(),
                AiEvaluation = x.Student.Attendances
                    .Where(a => a.SessionId == session.Id)
                    .Select(a => a.AiEvaluation)
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

                row.AttendanceId = overrideRow.AttendanceId ?? row.AttendanceId;
                row.HasAttendance = overrideRow.HasAttendance || row.HasAttendance;
                row.IsPresent = overrideRow.IsPresent;
                row.IsExcused = overrideRow.IsExcused;
                row.TeacherRawNote = overrideRow.TeacherRawNote;
                row.ProductMediaUrls = overrideRow.ProductMediaUrls;
                row.AiEvaluation = overrideRow.AiEvaluation ?? row.AiEvaluation;
            }
        }

        foreach (var row in studentRows)
        {
            row.MediaHint = string.IsNullOrWhiteSpace(row.ProductMediaUrls)
                ? "Tải video sản phẩm lên để làm minh chứng."
                : "Có thể tải lại video mới nếu cần phân tích lại.";

            row.StatusLabel = !row.HasAttendance
                ? "Chưa ghi nhận"
                : row.IsPresent
                    ? "Có mặt"
                    : row.IsExcused ? "Vắng phép" : "Vắng";

            row.StatusBadgeClass = !row.HasAttendance
                ? "teacher-tag teacher-tag--neutral"
                : row.IsPresent
                    ? "teacher-tag teacher-tag--success"
                    : row.IsExcused ? "teacher-tag teacher-tag--info" : "teacher-tag teacher-tag--warning";

            row.StatusHelpText = !row.HasAttendance
                ? "Học viên này chưa có bản ghi điểm danh cho buổi học."
                : row.IsPresent
                    ? "Đã được ghi nhận có mặt trong buổi học."
                    : row.IsExcused ? "Đã được ghi nhận vắng có phép trong buổi học." : "Đã được ghi nhận vắng trong buổi học.";
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
            AttendanceCount = studentRows.Count(x => x.HasAttendance),
            MissingCount = studentRows.Count(x => !x.HasAttendance),
            PresentCount = studentRows.Count(x => x.HasAttendance && x.IsPresent),
            AbsentCount = studentRows.Count(x => x.HasAttendance && !x.IsPresent),
            PayrollStatus = session.PayrollStatus,
            IsReadOnly = !string.IsNullOrWhiteSpace(editLockMessage),
            EditLockMessage = editLockMessage,
            Rows = studentRows
        };
    }

    private IActionResult WriteDenied(int sessionId, string message)
    {
        if (IsAjaxRequest())
        {
            return BadRequest(new { success = false, message });
        }

        TempData["ErrorMessage"] = message;
        return RedirectToAction(nameof(Board), new { sessionId });
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
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
            Date = item.Date,
            StudentCount = item.StudentCount,
            AttendanceCount = item.AttendanceCount,
            MissingAttendanceCount = Math.Max(0, item.StudentCount - item.AttendanceCount),
            CompletionText = item.StudentCount == 0
                ? "Lớp này chưa có học viên."
                : $"{item.AttendanceCount}/{item.StudentCount} học viên đã được ghi nhận.",
            StatusLabel = statusLabel,
            StatusBadgeClass = statusBadgeClass,
            ActionHint = item.Date > today
                ? "Buổi này chưa diễn ra."
                : item.StudentCount == 0
                    ? "Cần ghi danh học viên vào lớp trước khi điểm danh."
                    : item.AttendanceCount >= item.StudentCount
                        ? "Buổi này đã ghi nhận đủ học viên."
                        : $"Còn thiếu {Math.Max(0, item.StudentCount - item.AttendanceCount)} học viên chưa ghi nhận.",
            IsToday = item.Date == today,
            IsUpcoming = item.Date > today,
            IsCompleted = item.StudentCount > 0 && item.AttendanceCount >= item.StudentCount,
            HasStudents = item.StudentCount > 0,
            NeedsAttention = item.Date <= today && item.StudentCount > 0 && item.AttendanceCount < item.StudentCount
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
