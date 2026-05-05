using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ClassesController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;

    public ClassesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null, int page = 1)
    {
        var filters = new[]
        {
            new ClassFilterViewModel { Key = "all", Label = "Tất cả" },
            new ClassFilterViewModel { Key = "active", Label = "Đang mở" },
            new ClassFilterViewModel { Key = "upcoming", Label = "Sắp khai giảng" },
            new ClassFilterViewModel { Key = "completed", Label = "Đã kết thúc" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Classes
            .AsNoTracking()
            .Select(x => new ClassListProjection
            {
                Id = x.Id,
                ClassCode = x.ClassCode,
                CourseName = x.Course.Name,
                CourseCode = x.Course.Code,
                TeacherName = x.Teacher.FullName,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                EnrollmentCount = x.Enrollments.Count,
                SessionCount = x.Sessions.Count
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.ClassCode.Contains(searchTerm) ||
                x.CourseName.Contains(searchTerm) ||
                x.CourseCode.Contains(searchTerm) ||
                x.TeacherName.Contains(searchTerm));
        }

        query = ApplyFilter(query, normalizedFilter, today);

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var classes = await query
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var model = new ClassManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            TotalClasses = totalItems,
            CurrentPage = page,
            TotalPages = totalPages,
            Filters = filters,
            Classes = classes.Select(x => MapClassListItem(x, today)).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var model = await _context.Classes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ClassDetailsViewModel
            {
                Id = x.Id,
                ClassCode = x.ClassCode,
                CourseName = x.Course.Name,
                CourseCode = x.Course.Code,
                TeacherName = x.Teacher.FullName,
                TeacherEmail = x.Teacher.Email,
                TeacherPhone = x.Teacher.Phone,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                EnrollmentCount = x.Enrollments.Count,
                SessionCount = x.Sessions.Count,
                StatusLabel = x.StartDate > today
                    ? "Sắp khai giảng"
                    : x.EndDate.HasValue && x.EndDate.Value < today
                        ? "Đã kết thúc"
                        : "Đang mở",
                StatusBadgeClass = x.StartDate > today
                    ? "bg-[#fff4e8] text-[#9b682f]"
                    : x.EndDate.HasValue && x.EndDate.Value < today
                        ? "bg-[#eeeee9] text-[#42493d]"
                        : "bg-[#edf7e8] text-[#456c3f]",
                AgeRangeText = $"{x.Course.TargetAgeMin}-{x.Course.TargetAgeMax} tuổi",
                PriceText = $"{x.Course.Price:N0}đ",
                TotalSessionsText = $"{x.Course.TotalSessions} buổi",
                Students = x.Enrollments
                    .OrderBy(e => e.Student.FullName)
                    .Select(e => new ClassStudentSummaryViewModel
                    {
                        FullName = e.Student.FullName,
                        Username = e.Student.Username,
                        EnrollDateText = e.EnrollDate.ToString("dd/MM/yyyy")
                    })
                    .ToList(),
                Sessions = x.Sessions
                    .OrderBy(s => s.SessionNo)
                    .Select(s => new ClassSessionSummaryViewModel
                    {
                        SessionNo = s.SessionNo,
                        Date = s.Date,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Topic = s.Topic
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateClassViewModel();
        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClassViewModel model)
    {
        await PopulateOptionsAsync(model);
        var normalizedCode = model.ClassCode.Trim().ToUpperInvariant();

        if (await _context.Classes.AnyAsync(x => x.ClassCode.ToLower() == normalizedCode.ToLower()))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");
        }

        if (!await IsValidCourseAsync(model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), "Khóa học không hợp lệ.");
        }

        if (!await IsValidTeacherAsync(model.TeacherId))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "Giáo viên không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Class
        {
            CourseId = model.CourseId!.Value,
            TeacherId = model.TeacherId!.Value,
            ClassCode = normalizedCode,
            StartDate = model.StartDate!.Value,
            EndDate = model.EndDate
        };

        try
        {
            _context.Classes.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateClassCode(ex))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo lớp học mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditClassViewModel
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            TeacherId = entity.TeacherId,
            ClassCode = entity.ClassCode,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate
        };

        await PopulateOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditClassViewModel model)
    {
        await PopulateOptionsAsync(model);
        var entity = await _context.Classes.FirstOrDefaultAsync(x => x.Id == model.Id);

        if (entity == null)
        {
            return NotFound();
        }

        var normalizedCode = model.ClassCode.Trim().ToUpperInvariant();

        if (await _context.Classes.AnyAsync(x => x.Id != model.Id && x.ClassCode.ToLower() == normalizedCode.ToLower()))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");
        }

        if (!await IsValidCourseAsync(model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), "Khóa học không hợp lệ.");
        }

        if (!await IsValidTeacherAsync(model.TeacherId))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "Giáo viên không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entity.CourseId = model.CourseId!.Value;
        entity.TeacherId = model.TeacherId!.Value;
        entity.ClassCode = normalizedCode;
        entity.StartDate = model.StartDate!.Value;
        entity.EndDate = model.EndDate;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateClassCode(ex))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "Mã lớp đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật lớp học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.Classes
            .Include(x => x.Enrollments)
            .Include(x => x.Sessions)
            .Include(x => x.Invoices)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lớp học cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        if (entity.Enrollments.Count > 0 || entity.Sessions.Count > 0 || entity.Invoices.Count > 0)
        {
            TempData["ErrorMessage"] = "Không thể xóa lớp học này vì đang có học viên, buổi học hoặc hóa đơn liên quan.";
            return RedirectToAction(nameof(Index));
        }

        _context.Classes.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa lớp học.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(CreateClassViewModel model)
    {
        model.CourseOptions = await _context.Courses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Code})", x.Id.ToString()))
            .ToListAsync();

        model.TeacherOptions = await _context.Users
            .AsNoTracking()
            .Where(x => x.IsActive && (x.Role.Name == "Teacher" || x.Role.Name == "Giáo viên"))
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();
    }

    private async Task<bool> IsValidCourseAsync(int? courseId)
    {
        return courseId.HasValue && await _context.Courses.AnyAsync(x => x.Id == courseId.Value);
    }

    private async Task<bool> IsValidTeacherAsync(int? teacherId)
    {
        return teacherId.HasValue && await _context.Users.AnyAsync(x =>
            x.Id == teacherId.Value &&
            x.IsActive &&
            (x.Role.Name == "Teacher" || x.Role.Name == "Giáo viên"));
    }

    private static IQueryable<ClassListProjection> ApplyFilter(IQueryable<ClassListProjection> query, string filter, DateOnly today)
    {
        return filter switch
        {
            "active" => query.Where(x => x.StartDate <= today && (!x.EndDate.HasValue || x.EndDate.Value >= today)),
            "upcoming" => query.Where(x => x.StartDate > today),
            "completed" => query.Where(x => x.EndDate.HasValue && x.EndDate.Value < today),
            _ => query
        };
    }

    private static ClassManagementItemViewModel MapClassListItem(ClassListProjection item, DateOnly today)
    {
        var statusLabel = item.StartDate > today
            ? "Sắp khai giảng"
            : item.EndDate.HasValue && item.EndDate.Value < today
                ? "Đã kết thúc"
                : "Đang mở";

        var statusBadgeClass = item.StartDate > today
            ? "bg-[#fff4e8] text-[#9b682f]"
            : item.EndDate.HasValue && item.EndDate.Value < today
                ? "bg-[#eeeee9] text-[#42493d]"
                : "bg-[#edf7e8] text-[#456c3f]";

        var scheduleText = $"{item.StartDate:dd/MM/yyyy} - {(item.EndDate.HasValue ? item.EndDate.Value.ToString("dd/MM/yyyy") : "Đang mở")}";

        return new ClassManagementItemViewModel
        {
            Id = item.Id,
            ClassCode = item.ClassCode,
            CourseName = item.CourseName,
            CourseCode = item.CourseCode,
            TeacherName = item.TeacherName,
            ScheduleText = scheduleText,
            EnrollmentCount = item.EnrollmentCount,
            SessionCount = item.SessionCount,
            EnrollmentText = $"{item.EnrollmentCount} học viên · {item.SessionCount} buổi",
            StatusLabel = statusLabel,
            StatusBadgeClass = statusBadgeClass
        };
    }

    private static bool IsDuplicateClassCode(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627
            && sqlEx.Message.Contains("UQ__Classes__2ECD4A55", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ClassListProjection
    {
        public int Id { get; set; }
        public string ClassCode { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int EnrollmentCount { get; set; }
        public int SessionCount { get; set; }
    }
}
