using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Controllers;

public class CoursesController : Controller
{
    private readonly ApplicationDbContext _context;

    public CoursesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int page = 1, string? search = null)
    {
        int pageSize = 6;
        var query = _context.Courses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search));
        }
        
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var dbCourses = await query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        // Chuyển đổi dữ liệu từ DB sang ViewModel để hiển thị
        var viewModels = dbCourses.Select(c => new CourseSummaryViewModel
        {
            Id = c.Id,
            Slug = c.Code.ToLower(),
            Title = c.Name,
            Summary = !string.IsNullOrEmpty(c.Summary) ? c.Summary : $"Chương trình thực hành chuyên sâu dành cho độ tuổi từ {c.TargetAgeMin} đến {c.TargetAgeMax} tuổi.",
            // Nếu có ảnh trong CSDL thì dùng, không thì dùng ảnh mặc định
            ImageUrl = !string.IsNullOrEmpty(c.ImageUrl) ? c.ImageUrl : "/images/courses/default.jpg",
            Category = "STEM",
            Level = "Cơ bản",
            DurationText = $"{c.TotalSessions} buổi",
            PriceText = c.Price.ToString("N0") + "đ"
        }).ToList();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.SearchTerm = search;

        return View(viewModels);
    }

    public async Task<IActionResult> Details(string slug)
    {
        // Tìm khóa học trong DB theo Code (đóng vai trò như Slug)
        var dbCourse = await _context.Courses.FirstOrDefaultAsync(c => c.Code == slug);
        
        if (dbCourse == null)
        {
            return NotFound();
        }

        var courseSummary = new CourseSummaryViewModel
        {
            Id = dbCourse.Id,
            Slug = dbCourse.Code.ToLower(),
            Title = dbCourse.Name,
            Summary = !string.IsNullOrEmpty(dbCourse.Summary) ? dbCourse.Summary : $"Chương trình thực hành chuyên sâu dành cho độ tuổi từ {dbCourse.TargetAgeMin} đến {dbCourse.TargetAgeMax} tuổi.",
            ImageUrl = !string.IsNullOrEmpty(dbCourse.ImageUrl) ? dbCourse.ImageUrl : "/images/courses/default.jpg",
            Category = "STEM",
            Level = "Cơ bản",
            DurationText = $"{dbCourse.TotalSessions} buổi",
            PriceText = dbCourse.Price.ToString("N0") + "đ"
        };

        // Lấy 3 khóa học ngẫu nhiên làm Khóa học liên quan
        var relatedDbCourses = await _context.Courses
            .Where(c => c.Id != dbCourse.Id)
            .Take(3)
            .ToListAsync();

        var relatedViewModels = relatedDbCourses.Select(c => new CourseSummaryViewModel
        {
            Id = c.Id,
            Slug = c.Code.ToLower(),
            Title = c.Name,
            Summary = !string.IsNullOrEmpty(c.Summary) ? c.Summary : $"Chương trình từ {c.TargetAgeMin}-{c.TargetAgeMax} tuổi.",
            ImageUrl = !string.IsNullOrEmpty(c.ImageUrl) ? c.ImageUrl : "/images/courses/default.jpg",
            Category = "STEM",
            Level = "Cơ bản",
            DurationText = $"{c.TotalSessions} buổi",
            PriceText = c.Price.ToString("N0") + "đ"
        }).ToList();

        var viewModel = new CourseDetailViewModel
        {
            Course = courseSummary,
            HeroImageUrl = courseSummary.ImageUrl,
            BreadcrumbTitle = courseSummary.Title,
            SchoolLevel = $"Từ {dbCourse.TargetAgeMin} - {dbCourse.TargetAgeMax} tuổi",
            LessonCount = courseSummary.DurationText,
            PriceText = courseSummary.PriceText,
            InstructorName = "Cô Nguyễn Minh Thư",
            InstructorTitle = "Giảng viên STEM",
            InstructorBio = "Với hơn 8 năm kinh nghiệm giảng dạy STEM, cô Thư luôn biết cách biến những bài học khô khan thành những câu chuyện thú vị.",
            InstructorImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuCT9vlXrSw0jzlqDxNbx8qHjfTkBH7PduIXiAqvFeMaiDDk5BwuaEd-fWjlzm7NvKUalfND9wNsopg05O9IufiIyMBrjAZoQEXraYzJaYGg8sGgT7FDAnGpXjC08BYthZOP9AccjJguJAS2VhA6i1_hdem9CWp-DO2-CHBjeuwiCL2U0KUcp0h5zkP0NlBxRej1WUOzzIKEGF3idqB_CO-rWEcZVjmVOV46ok0uheaTXdTsqqVsaNSxu9vsr4w1xVpO7FDMSg7JRCMM",
            Benefits =
            [
                new CourseBenefitViewModel { Icon = "construction", Title = "Thực hành 100%", Description = "Kết hợp lý thuyết và thực hành." },
                new CourseBenefitViewModel { Icon = "psychology", Title = "Tư duy logic", Description = "Phát triển qua các dự án." },
                new CourseBenefitViewModel { Icon = "verified_user", Title = "Chứng chỉ", Description = "Nhận chứng nhận hoàn thành." }
            ],
            CurriculumItems =
            [
                "Làm quen với môn học",
                "Tương tác cơ bản",
                "Mô phỏng thực tế",
                "Dự án cuối khóa"
            ],
            RelatedCourses = relatedViewModels
        };

        return View(viewModel);
    }
}
