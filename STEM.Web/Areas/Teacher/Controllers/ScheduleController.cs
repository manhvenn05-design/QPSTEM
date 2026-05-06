using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class ScheduleController : Controller
{
    private readonly ApplicationDbContext _context;

    public ScheduleController(ApplicationDbContext context)
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

        var today = DateOnly.FromDateTime(DateTime.Today);
        var searchTerm = q?.Trim() ?? string.Empty;

        var query = _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId.Value)
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
                AttendanceCount = x.Attendances.Count
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

        var model = new TeacherScheduleIndexViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalSessions = sessions.Count,
            Filters = filters,
            Sessions = sessions.Select(x => new TeacherScheduleItemViewModel
            {
                SessionId = x.Id,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                ClassCode = x.ClassCode,
                CourseName = x.CourseName,
                ScheduleText = $"{x.Date:dd/MM/yyyy} · {x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                StudentCount = x.StudentCount,
                AttendanceCount = x.AttendanceCount,
                AttendancePercent = x.StudentCount == 0 ? 0 : (int)Math.Round((double)x.AttendanceCount * 100 / x.StudentCount),
                HasTeachingMaterial = !string.IsNullOrWhiteSpace(x.TeachingMaterialUrl),
                TeachingMaterialUrl = x.TeachingMaterialUrl ?? string.Empty,
                StatusLabel = x.Date > today ? "Sắp tới" : x.Date < today ? "Đã dạy" : "Hôm nay",
                StatusBadgeClass = x.Date > today
                    ? "teacher-tag teacher-tag--warning"
                    : x.Date < today
                        ? "teacher-tag teacher-tag--neutral"
                        : "teacher-tag teacher-tag--success"
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
            .Where(x => x.Id == id && x.Class.TeacherId == teacherId.Value)
            .Select(x => new TeacherScheduleDetailsViewModel
            {
                SessionId = x.Id,
                SessionLabel = $"Buổi số {x.SessionNo:00}",
                ClassCode = x.Class.ClassCode,
                CourseName = x.Class.Course.Name,
                TeacherName = x.Class.Teacher.FullName,
                DateText = x.Date.ToString("dd/MM/yyyy"),
                TimeRangeText = $"{x.StartTime:HH\\:mm} - {x.EndTime:HH\\:mm}",
                Topic = string.IsNullOrWhiteSpace(x.Topic) ? "Chưa cập nhật chủ đề" : x.Topic,
                TeachingMaterialUrl = x.TeachingMaterialUrl ?? string.Empty,
                AssistantNote = x.AssistantNote,
                StudentCount = x.Class.Enrollments.Count,
                AttendanceCount = x.Attendances.Count,
                EquipmentBorrowCount = x.EquipmentBorrows.Count,
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

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }
}
