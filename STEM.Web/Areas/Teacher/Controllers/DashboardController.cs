using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var nextWeek = today.AddDays(7);
        var vietnameseCulture = new CultureInfo("vi-VN");

        var todaySessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId.Value && x.Date == today)
            .OrderBy(x => x.StartTime)
            .Select(x => new TeacherDashboardSessionItemViewModel
            {
                SessionId = x.Id,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                ScheduleText = $"{x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                AttendancePercent = x.Class.Enrollments.Count == 0
                    ? 0
                    : (int)Math.Round((double)x.Attendances.Count * 100 / x.Class.Enrollments.Count)
            })
            .ToListAsync();

        var upcomingSessions = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId.Value && x.Date > today && x.Date <= nextWeek)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new TeacherDashboardSessionItemViewModel
            {
                SessionId = x.Id,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                ScheduleText = $"{x.Date:dd/MM} · {x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                AttendancePercent = x.Class.Enrollments.Count == 0
                    ? 0
                    : (int)Math.Round((double)x.Attendances.Count * 100 / x.Class.Enrollments.Count)
            })
            .Take(5)
            .ToListAsync();

        var evidenceQueue = await _context.Attendances
            .AsNoTracking()
            .Where(x => x.Session.Class.TeacherId == teacherId.Value &&
                        (string.IsNullOrWhiteSpace(x.TeacherRawNote) || string.IsNullOrWhiteSpace(x.ProductMediaUrls)))
            .OrderByDescending(x => x.Session.Date)
            .ThenBy(x => x.Student.FullName)
            .Select(x => new TeacherDashboardEvidenceItemViewModel
            {
                AttendanceId = x.Id,
                SessionId = x.SessionId,
                StudentName = x.Student.FullName,
                ClassCode = x.Session.Class.ClassCode,
                SessionLabel = $"Buổi số {x.Session.SessionNo:00}",
                StatusLabel = string.IsNullOrWhiteSpace(x.ProductMediaUrls)
                    ? (string.IsNullOrWhiteSpace(x.TeacherRawNote) ? "Thiếu ghi chú và media" : "Thiếu media")
                    : "Thiếu ghi chú",
                StatusBadgeClass = "teacher-tag teacher-tag--warning"
            })
            .Take(6)
            .ToListAsync();

        var activeBorrows = await _context.EquipmentBorrows
            .AsNoTracking()
            .Where(x => x.BorrowerId == teacherId.Value && x.ReturnTime == null)
            .OrderByDescending(x => x.BorrowTime)
            .Select(x => new TeacherDashboardBorrowItemViewModel
            {
                BorrowId = x.Id,
                EquipmentId = x.EquipmentId,
                EquipmentSerialNumber = x.Equipment.SerialNumber,
                ImageUrl = x.Equipment.ImageUrl,
                SessionLabel = $"Buổi số {x.Session.SessionNo:00} · {x.Session.Class.ClassCode}",
                BorrowTimeText = x.BorrowTime.ToString("dd/MM/yyyy HH:mm")
            })
            .Take(6)
            .ToListAsync();

        var pendingAttendanceCount = await _context.Sessions.CountAsync(x =>
            x.Class.TeacherId == teacherId.Value &&
            x.Date <= today &&
            x.Class.Enrollments.Count > 0 &&
            x.Attendances.Count < x.Class.Enrollments.Count);

        var teacherFullName = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == teacherId.Value)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync() ?? User.Identity?.Name ?? "Giáo viên";

        var model = new TeacherDashboardViewModel
        {
            TodayLabel = $"Hôm nay · {DateTime.Today.ToString("dddd, dd/MM/yyyy", vietnameseCulture)}",
            TeacherName = teacherFullName,
            ActiveClassCount = await _context.Classes.CountAsync(x =>
                x.TeacherId == teacherId.Value &&
                x.StartDate <= today &&
                x.EndDate >= today),
            TotalStudentCount = await _context.Enrollments.CountAsync(x => x.Class.TeacherId == teacherId.Value),
            TodaySessionCount = todaySessions.Count,
            PendingAttendanceCount = pendingAttendanceCount,
            UpcomingSessionCount = await _context.Sessions.CountAsync(x => x.Class.TeacherId == teacherId.Value && x.Date > today),
            ActiveBorrowCount = await _context.EquipmentBorrows.CountAsync(x => x.BorrowerId == teacherId.Value && x.ReturnTime == null),
            EvidenceReadyCount = await _context.Attendances.CountAsync(x =>
                x.Session.Class.TeacherId == teacherId.Value &&
                !string.IsNullOrWhiteSpace(x.TeacherRawNote) &&
                !string.IsNullOrWhiteSpace(x.ProductMediaUrls)),
            TodaySessions = todaySessions,
            UpcomingSessions = upcomingSessions,
            EvidenceQueue = evidenceQueue,
            ActiveBorrows = activeBorrows
        };

        ViewData["Title"] = "Bảng điều khiển";
        return View(model);
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }
}
