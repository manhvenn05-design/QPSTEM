using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;
using STEM.Web.Services;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class ScheduleController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public ScheduleController(ApplicationDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "today", string? q = null, DateTime? date = null, string view = "calendar")
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var filters = new[]
        {
            new TeacherScheduleFilterViewModel { Key = "today", Label = "Hôm nay" },
            new TeacherScheduleFilterViewModel { Key = "upcoming", Label = "Sắp tới" },
            new TeacherScheduleFilterViewModel { Key = "past", Label = "Đã dạy" },
            new TeacherScheduleFilterViewModel { Key = "all", Label = "Tất cả" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "today" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "today";
        }

        var normalizedView = string.Equals(view, "list", StringComparison.OrdinalIgnoreCase)
            ? "list"
            : "calendar";

        var today = DateOnly.FromDateTime(DateTime.Today);
        var searchTerm = q?.Trim() ?? string.Empty;
        var targetDateTime = date?.Date ?? DateTime.Today;
        var weekStart = GetWeekStart(targetDateTime);
        var weekEnd = weekStart.AddDays(6);
        var currentWeekStart = GetWeekStart(DateTime.Today);
        var weekStartDateOnly = DateOnly.FromDateTime(weekStart);
        var weekEndDateOnly = DateOnly.FromDateTime(weekEnd);

        var query = _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId.Value || x.SubstituteTeacherId == teacherId.Value)
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.Topic,
                x.TeachingMaterialUrl,
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                TeacherId = x.Class.TeacherId,
                SubstituteTeacherId = x.SubstituteTeacherId
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                (x.Topic != null && x.Topic.Contains(searchTerm)));
        }

        query = normalizedFilter switch
        {
            "today" => query.Where(x => x.Date == today),
            "upcoming" => query.Where(x => x.Date > today),
            "past" => query.Where(x => x.Date < today),
            _ => query
        };

        var sessions = await query
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.ClassCode)
            .ThenBy(x => x.SessionNo)
            .ToListAsync();

        var calendarRawSessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => (x.Class.TeacherId == teacherId.Value || x.SubstituteTeacherId == teacherId.Value) && x.Date >= weekStartDateOnly && x.Date <= weekEndDateOnly)
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.Topic,
                x.TeachingMaterialUrl,
                ClassCode    = x.Class.ClassCode,
                CourseName   = x.Class.Course.Name,
                TeacherName  = x.Class.Teacher.FullName,
                RoomName     = x.Room != null ? x.Room.Name : null,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                TeacherId = x.Class.TeacherId,
                SubstituteTeacherId = x.SubstituteTeacherId
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ToListAsync();

        var model = new TeacherScheduleIndexViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            SelectedView = normalizedView,
            TotalSessions = sessions.Count,
            TargetDate = targetDateTime,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            WeekLabel = $"{weekStart:dd/MM} - {weekEnd:dd/MM/yyyy}",
            IsCurrentWeek = weekStart == currentWeekStart,
            Filters = filters,
            Sessions = sessions.Select(x => new TeacherScheduleItemViewModel
            {
                SessionId = x.Id,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                ClassCode = x.ClassCode,
                CourseName = x.CourseName,
                Date = x.Date.ToDateTime(TimeOnly.MinValue),
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                ScheduleText = $"{x.Date:dd/MM/yyyy} · {x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                StudentCount = x.StudentCount,
                AttendanceCount = x.AttendanceCount,
                AttendancePercent = x.StudentCount == 0 ? 0 : (int)Math.Round((double)x.AttendanceCount * 100 / x.StudentCount),
                HasTeachingMaterial = !string.IsNullOrWhiteSpace(x.TeachingMaterialUrl),
                TeachingMaterialUrl = x.TeachingMaterialUrl ?? string.Empty,
                IsSubstituteAssignedToOther = x.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue,
                IsSubstituteAssignedToMe = x.SubstituteTeacherId == teacherId.Value,
                StatusLabel = (x.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue) ? "Đã giao dạy thay" : x.Date > today ? "Sắp tới" : x.Date < today ? "Đã dạy" : "Hôm nay",
                StatusBadgeClass = (x.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue) ? "teacher-tag teacher-tag--neutral" : x.Date > today
                    ? "teacher-tag teacher-tag--warning"
                    : x.Date < today
                        ? "teacher-tag teacher-tag--neutral"
                        : "teacher-tag teacher-tag--info"
            }).ToList(),
            CalendarSessions = calendarRawSessions.Select(x => new TeacherScheduleItemViewModel
            {
                SessionId            = x.Id,
                SessionLabel         = $"Buổi số {x.SessionNo:00}",
                ClassCode            = x.ClassCode,
                CourseName           = x.CourseName,
                Date                 = x.Date.ToDateTime(TimeOnly.MinValue),
                StartTime            = x.StartTime,
                EndTime              = x.EndTime,
                ScheduleText         = $"{x.StartTime:HH\\:mm} – {x.EndTime:HH\\:mm}",
                Topic                = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                StudentCount         = x.StudentCount,
                AttendanceCount      = x.AttendanceCount,
                AttendancePercent    = x.StudentCount == 0 ? 0 : (int)Math.Round((double)x.AttendanceCount * 100 / x.StudentCount),
                HasTeachingMaterial  = !string.IsNullOrWhiteSpace(x.TeachingMaterialUrl),
                TeachingMaterialUrl  = x.TeachingMaterialUrl ?? string.Empty,
                TeacherName          = x.TeacherName,
                RoomName             = x.RoomName,
                IsSubstituteAssignedToOther = x.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue,
                IsSubstituteAssignedToMe = x.SubstituteTeacherId == teacherId.Value,
                StatusLabel          = (x.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue) ? "Giao dạy thay" : x.Date > today ? "Sắp tới" : x.Date < today ? "Đã dạy" : "Hôm nay",
                StatusBadgeClass     = (x.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue) ? "past" : x.Date > today ? "upcoming" : x.Date < today ? "past" : "today"
            }).ToList()
        };

        ViewData["Title"] = "Lịch dạy";
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

        var model = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == id && (x.Class.TeacherId == teacherId.Value || x.SubstituteTeacherId == teacherId.Value))
            .Select(x => new TeacherScheduleDetailsViewModel
            {
                SessionId = x.Id,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.SubstituteTeacherId.HasValue
                    ? x.SubstituteTeacher!.FullName + " (Dạy thay)"
                    : x.Class.Teacher.FullName,
                DateText = x.Date.ToString("dd/MM/yyyy"),
                TimeRangeText = $"{x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                TeachingMaterialUrl = x.TeachingMaterialUrl ?? string.Empty,
                AssistantNote = x.AssistantNote,
                ClassMediaUrls = x.ClassMediaUrls,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                EquipmentBorrowCount = x.EquipmentBorrows.Count,
                IsSubstituteAssignedToOther = x.Class.TeacherId == teacherId.Value && x.SubstituteTeacherId.HasValue,
                Students = x.Class.Enrollments
                    .OrderBy(e => e.Student.FullName)
                    .Select(e => new TeacherScheduleStudentItemViewModel
                    {
                        FullName = e.Student.FullName,
                        Username = e.Student.Username
                    })
                    .ToList(),
                Equipments = x.EquipmentBorrows
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => new TeacherScheduleEquipmentItemViewModel
                    {
                        SerialNumber = b.Equipment.SerialNumber,
                        BorrowerName = b.Borrower.FullName,
                        BorrowTimeText = b.BorrowTime.ToString("dd/MM/yyyy HH:mm"),
                        ReturnTimeText = b.ReturnTime.HasValue ? b.ReturnTime.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa trả"
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Lịch dạy";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Viewer(int id)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var sessionData = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == id && (x.Class.TeacherId == teacherId.Value || x.SubstituteTeacherId == teacherId.Value))
            .Select(x => new
            {
                x.Id,
                x.SessionNo,
                x.Topic,
                x.TeachingMaterialUrl,
                x.SubstituteTeacherId,
                ClassTeacherId = x.Class.TeacherId
            })
            .FirstOrDefaultAsync();

        if (sessionData == null || string.IsNullOrWhiteSpace(sessionData.TeachingMaterialUrl))
        {
            return NotFound("Không tìm thấy giáo án cho buổi học này.");
        }

        // Giáo viên chính đã giao thay → không được vào Viewer
        if (sessionData.ClassTeacherId == teacherId.Value && sessionData.SubstituteTeacherId.HasValue && sessionData.SubstituteTeacherId.Value != teacherId.Value)
        {
            return NotFound("Buổi học này đã được giao cho giáo viên khác dạy thay.");
        }

        ViewBag.MaterialUrl  = sessionData.TeachingMaterialUrl;
        ViewBag.SessionLabel = $"Buổi số {sessionData.SessionNo:00}";
        ViewBag.Topic        = sessionData.Topic;

        return View();
    }

    // PdfProxy đã không còn cần thiết vì file được lưu local trong wwwroot/uploads/
    // và được ASP.NET Core static files middleware phục vụ trực tiếp mà không cần proxy.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotes(TeacherUpdateSessionNotesViewModel model)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        // Kiểm tra nhanh bằng projection trước khi load entity
        var sessionCheck = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Id == model.SessionId && (x.Class.TeacherId == teacherId.Value || x.SubstituteTeacherId == teacherId.Value))
            .Select(x => new { x.SubstituteTeacherId, ClassTeacherId = x.Class.TeacherId })
            .FirstOrDefaultAsync();

        if (sessionCheck == null)
        {
            return NotFound();
        }

        // Chặn giáo viên chính nếu đã giao thay
        if (sessionCheck.ClassTeacherId == teacherId.Value && sessionCheck.SubstituteTeacherId.HasValue && sessionCheck.SubstituteTeacherId.Value != teacherId.Value)
        {
            TempData["ErrorMessage"] = "Buổi này đã được giao cho giáo viên khác dạy thay.";
            return RedirectToAction(nameof(Details), new { id = model.SessionId });
        }

        // Load entity thực để cập nhật
        var session = await _context.Sessions
            .Where(x => x.Id == model.SessionId)
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return NotFound();
        }

        var uploadedMediaUrls = new List<string>();

        try
        {
            foreach (var file in Request.Form.Files.Where(x => string.Equals(x.Name, "ClassMediaFiles", StringComparison.Ordinal) && x.Length > 0))
            {
                uploadedMediaUrls.Add(await _fileStorage.SaveFileAsync(file, "session-media"));
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = model.SessionId });
        }

        session.ClassMediaUrls = MergeMediaUrls(model.ClassMediaUrls, uploadedMediaUrls);
        session.AssistantNote = model.AssistantNote;
        
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã lưu ghi chú và media thành công.";
        return RedirectToAction(nameof(Details), new { id = model.SessionId });
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    private static string? MergeMediaUrls(string? rawUrls, IEnumerable<string> uploadedUrls)
    {
        var mergedUrls = new List<string>();

        if (!string.IsNullOrWhiteSpace(rawUrls))
        {
            mergedUrls.AddRange(
                rawUrls.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        mergedUrls.AddRange(uploadedUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));

        return mergedUrls.Count == 0
            ? null
            : string.Join(Environment.NewLine, mergedUrls.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
