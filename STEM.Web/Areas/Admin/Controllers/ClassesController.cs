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
            new ClassFilterViewModel { Key = "all", Label = "Táº¥t cáº£" },
            new ClassFilterViewModel { Key = "active", Label = "Äang má»Ÿ" },
            new ClassFilterViewModel { Key = "upcoming", Label = "Sáº¯p khai giáº£ng" },
            new ClassFilterViewModel { Key = "completed", Label = "ÄÃ£ káº¿t thÃºc" }
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
    public async Task<IActionResult> Details(int id, string? studentSearch = null)
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
                    ? "Sáº¯p khai giáº£ng"
                    : x.EndDate.HasValue && x.EndDate.Value < today
                        ? "ÄÃ£ káº¿t thÃºc"
                        : "Äang má»Ÿ",
                StatusBadgeClass = x.StartDate > today
                    ? "bg-[#fff4e8] text-[#9b682f]"
                    : x.EndDate.HasValue && x.EndDate.Value < today
                        ? "bg-[#eeeee9] text-[#42493d]"
                        : "bg-[#edf7e8] text-[#456c3f]",
                AgeRangeText = $"{x.Course.TargetAgeMin}-{x.Course.TargetAgeMax} tuá»•i",
                PriceText = $"{x.Course.Price:N0}Ä‘",
                TotalSessionsText = $"{x.Course.TotalSessions} buá»•i",
                Students = x.Enrollments
                    .OrderBy(e => e.Student.FullName)
                    .Select(e => new ClassStudentSummaryViewModel
                    {
                        StudentId = e.StudentId,
                        FullName = e.Student.FullName,
                        Username = e.Student.Username,
                        EnrollDateText = e.EnrollDate.ToString("dd/MM/yyyy")
                    })
                    .ToList(),
                Sessions = x.Sessions
                    .OrderBy(s => s.SessionNo)
                    .Select(s => new ClassSessionSummaryViewModel
                    {
                        Id = s.Id,
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

        var normalizedStudentSearch = studentSearch?.Trim() ?? string.Empty;
        var availableStudents = await GetAvailableStudentsAsync(id, normalizedStudentSearch);
        model.StudentSearchTerm = normalizedStudentSearch;
        model.AvailableStudents = availableStudents;
        model.AvailableStudentCount = availableStudents.Count;

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
    public async Task<IActionResult> EnrollStudent(int id, int? studentId)
    {
        if (!studentId.HasValue)
        {
            TempData["ErrorMessage"] = "Vui lÃ²ng chá»n há»c viÃªn.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await _context.Classes.AnyAsync(x => x.Id == id))
        {
            return NotFound();
        }

        var isValidStudent = await _context.Users.AnyAsync(x =>
            x.Id == studentId.Value &&
            x.IsActive &&
            (x.Role.Name == "Student" || x.Role.Name == "Há»c sinh"));

        if (!isValidStudent)
        {
            TempData["ErrorMessage"] = "Há»c viÃªn khÃ´ng há»£p lá»‡.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _context.Enrollments.AnyAsync(x => x.ClassId == id && x.StudentId == studentId.Value))
        {
            TempData["ErrorMessage"] = "Há»c viÃªn nÃ y Ä‘Ã£ cÃ³ trong lá»›p.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Enrollments.Add(new Enrollment
        {
            ClassId = id,
            StudentId = studentId.Value
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "ÄÃ£ thÃªm há»c viÃªn vÃ o lá»›p.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrollStudents(int id, List<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t há»c viÃªn.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await _context.Classes.AnyAsync(x => x.Id == id))
        {
            return NotFound();
        }

        var validStudentIds = await _context.Users
            .AsNoTracking()
            .Where(x =>
                studentIds.Contains(x.Id) &&
                x.IsActive &&
                (x.Role.Name == "Student" || x.Role.Name == "Há»c sinh"))
            .Select(x => x.Id)
            .ToListAsync();

        if (validStudentIds.Count == 0)
        {
            TempData["ErrorMessage"] = "KhÃ´ng cÃ³ há»c viÃªn há»£p lá»‡ Ä‘á»ƒ thÃªm vÃ o lá»›p.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var enrolledIds = await _context.Enrollments
            .AsNoTracking()
            .Where(x => x.ClassId == id && validStudentIds.Contains(x.StudentId))
            .Select(x => x.StudentId)
            .ToListAsync();

        var newIds = validStudentIds.Except(enrolledIds).ToList();
        if (newIds.Count == 0)
        {
            TempData["ErrorMessage"] = "CÃ¡c há»c viÃªn Ä‘Ã£ chá»n Ä‘á»u Ä‘Ã£ cÃ³ trong lá»›p.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Enrollments.AddRange(newIds.Select(studentId => new Enrollment
        {
            ClassId = id,
            StudentId = studentId
        }));

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"ÄÃ£ thÃªm {newIds.Count} há»c viÃªn vÃ o lá»›p.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStudent(int id, int studentId)
    {
        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(x => x.ClassId == id && x.StudentId == studentId);
        if (enrollment == null)
        {
            TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y há»c viÃªn trong lá»›p nÃ y.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _context.Attendances.AnyAsync(x => x.StudentId == studentId && x.Session.ClassId == id))
        {
            TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ gá»¡ há»c viÃªn nÃ y vÃ¬ Ä‘Ã£ cÃ³ Ä‘iá»ƒm danh trong lá»›p.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await _context.Invoices.AnyAsync(x => x.ClassId == id && x.StudentId == studentId))
        {
            TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ gá»¡ há»c viÃªn nÃ y vÃ¬ Ä‘Ã£ cÃ³ hÃ³a Ä‘Æ¡n liÃªn quan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "ÄÃ£ gá»¡ há»c viÃªn khá»i lá»›p.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClassViewModel model)
    {
        await PopulateOptionsAsync(model);
        var normalizedCode = model.ClassCode.Trim().ToUpperInvariant();

        if (await _context.Classes.AnyAsync(x => x.ClassCode.ToLower() == normalizedCode.ToLower()))
        {
            ModelState.AddModelError(nameof(model.ClassCode), "MÃ£ lá»›p Ä‘Ã£ tá»“n táº¡i.");
        }

        if (!await IsValidCourseAsync(model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), "KhÃ³a há»c khÃ´ng há»£p lá»‡.");
        }

        if (!await IsValidTeacherAsync(model.TeacherId))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "GiÃ¡o viÃªn khÃ´ng há»£p lá»‡.");
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
            ModelState.AddModelError(nameof(model.ClassCode), "MÃ£ lá»›p Ä‘Ã£ tá»“n táº¡i.");
            return View(model);
        }

        TempData["SuccessMessage"] = "ÄÃ£ táº¡o lá»›p há»c má»›i.";
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
            ModelState.AddModelError(nameof(model.ClassCode), "MÃ£ lá»›p Ä‘Ã£ tá»“n táº¡i.");
        }

        if (!await IsValidCourseAsync(model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), "KhÃ³a há»c khÃ´ng há»£p lá»‡.");
        }

        if (!await IsValidTeacherAsync(model.TeacherId))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "GiÃ¡o viÃªn khÃ´ng há»£p lá»‡.");
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
            ModelState.AddModelError(nameof(model.ClassCode), "MÃ£ lá»›p Ä‘Ã£ tá»“n táº¡i.");
            return View(model);
        }

        TempData["SuccessMessage"] = "ÄÃ£ cáº­p nháº­t lá»›p há»c.";
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
            TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y lá»›p há»c cáº§n xÃ³a.";
            return RedirectToAction(nameof(Index));
        }

        if (entity.Enrollments.Count > 0 || entity.Sessions.Count > 0 || entity.Invoices.Count > 0)
        {
            TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ xÃ³a lá»›p há»c nÃ y vÃ¬ Ä‘ang cÃ³ há»c viÃªn, buá»•i há»c hoáº·c hÃ³a Ä‘Æ¡n liÃªn quan.";
            return RedirectToAction(nameof(Index));
        }

        _context.Classes.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "ÄÃ£ xÃ³a lá»›p há»c.";
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
            .Where(x => x.IsActive && (x.Role.Name == "Teacher" || x.Role.Name == "GiÃ¡o viÃªn"))
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString()))
            .ToListAsync();
    }

    private async Task<IReadOnlyList<ClassAvailableStudentViewModel>> GetAvailableStudentsAsync(int classId, string searchTerm)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (x.Role.Name == "Student" || x.Role.Name == "Há»c sinh") &&
                !x.Enrollments.Any(e => e.ClassId == classId));

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.FullName.Contains(searchTerm) ||
                x.Username.Contains(searchTerm));
        }

        return await query
            .OrderBy(x => x.FullName)
            .Select(x => new ClassAvailableStudentViewModel
            {
                StudentId = x.Id,
                FullName = x.FullName,
                Username = x.Username,
                SchoolName = x.StudentProfile != null ? x.StudentProfile.CurrentSchool : null
            })
            .Take(100)
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
            (x.Role.Name == "Teacher" || x.Role.Name == "GiÃ¡o viÃªn"));
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
            ? "Sáº¯p khai giáº£ng"
            : item.EndDate.HasValue && item.EndDate.Value < today
                ? "ÄÃ£ káº¿t thÃºc"
                : "Äang má»Ÿ";

        var statusBadgeClass = item.StartDate > today
            ? "bg-[#fff4e8] text-[#9b682f]"
            : item.EndDate.HasValue && item.EndDate.Value < today
                ? "bg-[#eeeee9] text-[#42493d]"
                : "bg-[#edf7e8] text-[#456c3f]";

        var scheduleText = $"{item.StartDate:dd/MM/yyyy} - {(item.EndDate.HasValue ? item.EndDate.Value.ToString("dd/MM/yyyy") : "Äang má»Ÿ")}";

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
            EnrollmentText = $"{item.EnrollmentCount} há»c viÃªn Â· {item.SessionCount} buá»•i",
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

