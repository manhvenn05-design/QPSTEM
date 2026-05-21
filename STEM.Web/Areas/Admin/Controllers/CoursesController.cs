using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using STEM.Web.Services;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CoursesController : Controller
{
    private const int PageSize = 10;
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public CoursesController(ApplicationDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string difficulty = "all", string? q = null, int page = 1)
    {
        var filters = new[]
        {
            new CourseFilterViewModel { Key = "all", Label = "Tất cả" },
            new CourseFilterViewModel { Key = "active", Label = "Đang diễn ra" },
            new CourseFilterViewModel { Key = "upcoming", Label = "Sắp khai giảng" },
            new CourseFilterViewModel { Key = "draft", Label = "Chưa mở lớp" }
        };
        var difficultyFilters = new[]
        {
            new CourseFilterViewModel { Key = "all", Label = "Tất cả nhóm môn" },
            new CourseFilterViewModel { Key = "1", Label = "Môn cơ bản" },
            new CourseFilterViewModel { Key = "2", Label = "Môn nâng cao" },
            new CourseFilterViewModel { Key = "3", Label = "Môn chuyên sâu" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "all";
        }
        var normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "all" : difficulty.Trim().ToLowerInvariant();
        if (difficultyFilters.All(x => x.Key != normalizedDifficulty))
        {
            normalizedDifficulty = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.Courses
            .AsNoTracking()
            .Select(x => new CourseListProjection
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                TargetAgeMin = x.TargetAgeMin,
                TargetAgeMax = x.TargetAgeMax,
                Price = x.Price,
                TotalSessions = x.TotalSessions,
                DifficultyLevel = x.DifficultyLevel,
                ImageUrl = x.ImageUrl,
                ClassCount = x.Classes.Count,
                EnrollmentCount = x.Classes.SelectMany(c => c.Enrollments).Count(),
                HasActiveClass = x.Classes.Any(c => c.StartDate <= today && (!c.EndDate.HasValue || c.EndDate.Value >= today)),
                HasUpcomingClass = x.Classes.Any(c => c.StartDate > today)
            });

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x => x.Name.Contains(searchTerm) || x.Code.Contains(searchTerm));
        }

        query = ApplyFilter(query, normalizedFilter);
        query = ApplyDifficultyFilter(query, normalizedDifficulty);

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var courses = await query
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var model = new CourseManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SelectedDifficulty = normalizedDifficulty,
            SearchTerm = searchTerm,
            TotalCourses = totalItems,
            CurrentPage = page,
            TotalPages = totalPages,
            Filters = filters,
            DifficultyFilters = difficultyFilters,
            Courses = courses.Select(MapCourseListItem).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var course = await _context.Courses
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CourseDetailsViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                TargetAgeMin = x.TargetAgeMin,
                TargetAgeMax = x.TargetAgeMax,
                Price = x.Price,
                TotalSessions = x.TotalSessions,
                DifficultyLevel = x.DifficultyLevel,
                DifficultyLabel = GetDifficultyLabel(x.DifficultyLevel),
                ImageUrl = x.ImageUrl,
                Summary = x.Summary,
                TotalClasses = x.Classes.Count,
                TotalEnrollments = x.Classes.SelectMany(c => c.Enrollments).Count(),
                StatusLabel = x.Classes.Any(c => c.StartDate <= today && (!c.EndDate.HasValue || c.EndDate.Value >= today))
                    ? "Đang diễn ra"
                    : x.Classes.Any(c => c.StartDate > today)
                        ? "Sắp khai giảng"
                        : x.Classes.Any()
                            ? "Đã kết thúc"
                            : "Chưa mở lớp",
                StatusBadgeClass = x.Classes.Any(c => c.StartDate <= today && (!c.EndDate.HasValue || c.EndDate.Value >= today))
                    ? "bg-[#edf7e8] text-[#5b8d3f]"
                    : x.Classes.Any(c => c.StartDate > today)
                        ? "bg-[#fff4e8] text-[#b6763f]"
                        : x.Classes.Any()
                            ? "bg-[#eef1f4] text-[#5f7383]"
                            : "bg-[#f4f5ef] text-[#7a8175]",
                Classes = x.Classes
                    .OrderByDescending(c => c.StartDate)
                    .Select(c => new CourseClassSummaryViewModel
                    {
                        ClassCode = c.ClassCode,
                        TeacherName = c.Teacher.FullName,
                        StartDate = c.StartDate,
                        EndDate = c.EndDate,
                        EnrollmentCount = c.Enrollments.Count
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (course == null)
        {
            return NotFound();
        }

        return View(course);
    }

    [HttpGet]
    public IActionResult Create(string? returnUrl = null)
    {
        return View(new CreateCourseViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseViewModel model)
    {
        var normalizedCode = model.Code.Trim().ToUpperInvariant();
        var normalizedName = model.Name.Trim();

        if (await _context.Courses.AnyAsync(x => x.Code.ToLower() == normalizedCode.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã khóa học đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? uploadedImageUrl = null;
        try
        {
            if (model.ImageFile != null)
            {
                uploadedImageUrl = await _fileStorage.SaveFileAsync(model.ImageFile, "courses");
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
            return View(model);
        }

        var course = new Course
        {
            Code = normalizedCode,
            Name = normalizedName,
            TargetAgeMin = model.TargetAgeMin,
            TargetAgeMax = model.TargetAgeMax,
            Price = model.Price,
            TotalSessions = model.TotalSessions,
            DifficultyLevel = model.DifficultyLevel,
            ImageUrl = uploadedImageUrl,
            Summary = string.IsNullOrWhiteSpace(model.Summary) ? null : model.Summary.Trim()
        };

        try
        {
            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateCourseCode(ex))
        {
            if (uploadedImageUrl != null)
            {
                await _fileStorage.DeleteFileAsync(uploadedImageUrl);
            }

            ModelState.AddModelError(nameof(model.Code), "Mã khóa học đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã xóa khóa học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, string? returnUrl = null)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (course == null)
        {
            return NotFound();
        }

        var model = new EditCourseViewModel
        {
            Id = course.Id,
            ReturnUrl = returnUrl,
            Code = course.Code,
            Name = course.Name,
            TargetAgeMin = course.TargetAgeMin,
            TargetAgeMax = course.TargetAgeMax,
            Price = course.Price,
            TotalSessions = course.TotalSessions,
            DifficultyLevel = course.DifficultyLevel is >= 1 and <= 3 ? course.DifficultyLevel : 1,
            ImageUrl = course.ImageUrl,
            CurrentImageUrl = course.ImageUrl,
            Summary = course.Summary
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCourseViewModel model)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (course == null)
        {
            return NotFound();
        }

        model.CurrentImageUrl = course.ImageUrl;

        var normalizedCode = model.Code.Trim().ToUpperInvariant();
        var normalizedName = model.Name.Trim();

        if (await _context.Courses.AnyAsync(x => x.Id != model.Id && x.Code.ToLower() == normalizedCode.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã khóa học đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var previousImageUrl = course.ImageUrl;
        string? uploadedImageUrl = null;

        try
        {
            if (model.ImageFile != null)
            {
                uploadedImageUrl = await _fileStorage.SaveFileAsync(model.ImageFile, "courses");
                course.ImageUrl = uploadedImageUrl;
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
            return View(model);
        }

        course.Code = normalizedCode;
        course.Name = normalizedName;
        course.TargetAgeMin = model.TargetAgeMin;
        course.TargetAgeMax = model.TargetAgeMax;
        course.Price = model.Price;
        course.TotalSessions = model.TotalSessions;
        course.DifficultyLevel = model.DifficultyLevel;
        course.Summary = string.IsNullOrWhiteSpace(model.Summary) ? null : model.Summary.Trim();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateCourseCode(ex))
        {
            if (uploadedImageUrl != null)
            {
                await _fileStorage.DeleteFileAsync(uploadedImageUrl);
                course.ImageUrl = previousImageUrl;
            }

            ModelState.AddModelError(nameof(model.Code), "Mã khóa học đã tồn tại.");
            return View(model);
        }

        if (uploadedImageUrl != null && previousImageUrl != uploadedImageUrl)
        {
            if (previousImageUrl != null) await _fileStorage.DeleteFileAsync(previousImageUrl);
        }

        TempData["SuccessMessage"] = "Đã cập nhật khóa học.";
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _context.Courses
            .Include(x => x.Classes)
            .Include(x => x.Leads)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        if (course.Classes.Count > 0 || course.Leads.Count > 0)
        {
            var hint = course.Classes.Count > 0
                ? $"Khóa học này đang có {course.Classes.Count} lớp học. Vào trang Lớp học, tìm theo mã \"{course.Code}\" và xóa từng lớp trước."
                : "Khóa học này đang có dữ liệu quan tâm (Leads) liên quan.";
            TempData["ErrorMessage"] = hint;
            return RedirectToAction(nameof(Index));
        }

        // Xóa Leads trước (không có con)
        if (course.Leads.Count > 0)
        {
            _context.Leads.RemoveRange(course.Leads);
        }

        var imageUrl = course.ImageUrl;
        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        if (imageUrl != null) await _fileStorage.DeleteFileAsync(imageUrl);

        TempData["SuccessMessage"] = "Đã xóa khóa học.";
        return RedirectToAction(nameof(Index));
    }

    private static IQueryable<CourseListProjection> ApplyFilter(IQueryable<CourseListProjection> query, string filter)
    {
        return filter switch
        {
            "active" => query.Where(x => x.HasActiveClass),
            "upcoming" => query.Where(x => !x.HasActiveClass && x.HasUpcomingClass),
            "draft" => query.Where(x => x.ClassCount == 0),
            _ => query
        };
    }

    private static IQueryable<CourseListProjection> ApplyDifficultyFilter(IQueryable<CourseListProjection> query, string difficulty)
    {
        return difficulty switch
        {
            "1" => query.Where(x => x.DifficultyLevel == 1),
            "2" => query.Where(x => x.DifficultyLevel == 2),
            "3" => query.Where(x => x.DifficultyLevel == 3),
            _ => query
        };
    }

    private static CourseManagementItemViewModel MapCourseListItem(CourseListProjection course)
    {
        var statusLabel = course.HasActiveClass
            ? "Đang diễn ra"
            : course.HasUpcomingClass
                ? "Sắp khai giảng"
                : course.ClassCount == 0
                    ? "Chưa mở lớp"
                    : "Đã kết thúc";

        var statusBadgeClass = course.HasActiveClass
            ? "bg-[#edf7e8] text-[#5b8d3f]"
            : course.HasUpcomingClass
                ? "bg-[#fff4e8] text-[#b6763f]"
                : course.ClassCount == 0
                    ? "bg-[#f4f5ef] text-[#7a8175]"
                    : "bg-[#eef1f4] text-[#5f7383]";

        return new CourseManagementItemViewModel
        {
            Id = course.Id,
            Code = course.Code,
            Name = course.Name,
            AgeRange = $"{course.TargetAgeMin}-{course.TargetAgeMax} tuổi",
            PriceText = $"{course.Price:N0}đ",
            TotalSessionsText = $"{course.TotalSessions} buổi",
            ClassCount = course.ClassCount,
            EnrollmentCount = course.EnrollmentCount,
            DifficultyLevel = course.DifficultyLevel,
            DifficultyLabel = GetDifficultyLabel(course.DifficultyLevel),
            StatusLabel = statusLabel,
            StatusBadgeClass = statusBadgeClass,
            ImageUrl = string.IsNullOrWhiteSpace(course.ImageUrl) ? null : course.ImageUrl
        };
    }

    private static bool IsDuplicateCourseCode(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627
            && sqlEx.Message.Contains("UQ__Courses__A25C5AA769A22348", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CourseListProjection
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public byte TargetAgeMin { get; set; }
        public byte TargetAgeMax { get; set; }
        public decimal Price { get; set; }
        public int TotalSessions { get; set; }
        public int DifficultyLevel { get; set; }
        public string? ImageUrl { get; set; }
        public int ClassCount { get; set; }
        public int EnrollmentCount { get; set; }
        public bool HasActiveClass { get; set; }
        public bool HasUpcomingClass { get; set; }
    }

    private static string GetDifficultyLabel(int difficultyLevel) => difficultyLevel switch
    {
        1 => "Môn cơ bản",
        2 => "Môn nâng cao",
        3 => "Môn chuyên sâu",
        _ => "Chưa phân loại"
    };
}



