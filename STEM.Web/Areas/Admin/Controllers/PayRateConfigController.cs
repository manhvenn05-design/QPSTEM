using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PayRateConfigController : Controller
{
    private readonly ApplicationDbContext _context;

    public PayRateConfigController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var configs = await _context.Set<PayRateConfig>()
            .AsNoTracking()
            .OrderBy(x => x.TeacherTier)
            .ThenBy(x => x.CourseDifficulty)
            .ToListAsync();

        var teachers = await _context.Users
            .AsNoTracking()
            .Where(x => x.Role.Name == "Teacher" || x.Role.Name == "Giáo viên")
            .OrderBy(x => x.FullName)
            .Select(x => new TeacherSalaryAssignmentViewModel
            {
                UserId = x.Id,
                FullName = x.FullName,
                Username = x.Username,
                SalaryTier = x.TeacherProfile != null ? x.TeacherProfile.SalaryTier : null,
                CustomSessionRate = x.TeacherProfile != null ? x.TeacherProfile.CustomSessionRate : null,
                HasTeacherProfile = x.TeacherProfile != null
            })
            .ToListAsync();

        var courses = await _context.Courses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new CourseDifficultyAssignmentViewModel
            {
                CourseId = x.Id,
                Code = x.Code,
                Name = x.Name,
                DifficultyLevel = x.DifficultyLevel,
                DifficultyLabel = GetDifficultyLabel(x.DifficultyLevel),
                TotalClasses = x.Classes.Count
            })
            .ToListAsync();

        var model = new PayRateConfigIndexViewModel
        {
            Configs = configs.Select(x => new PayRateConfigItemViewModel
            {
                Id = x.Id,
                TeacherTier = x.TeacherTier,
                CourseDifficulty = x.CourseDifficulty,
                RatePerSession = x.RatePerSession
            }).ToList(),
            Teachers = teachers,
            Courses = courses
        };

        if (model.Configs.Count == 0)
        {
            model.Configs = GenerateDefaultMatrix();
        }

        ViewData["Title"] = "Thiết lập lương giáo viên";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] List<PayRateConfigItemViewModel> input)
    {
        if (input == null || !input.Any())
        {
            return BadRequest("Dữ liệu không hợp lệ.");
        }

        var existingConfigs = await _context.Set<PayRateConfig>().ToListAsync();

        foreach (var item in input)
        {
            var existing = existingConfigs.FirstOrDefault(x =>
                x.TeacherTier == item.TeacherTier && x.CourseDifficulty == item.CourseDifficulty);

            if (existing != null)
            {
                existing.RatePerSession = item.RatePerSession;
            }
            else
            {
                _context.Set<PayRateConfig>().Add(new PayRateConfig
                {
                    TeacherTier = item.TeacherTier,
                    CourseDifficulty = item.CourseDifficulty,
                    RatePerSession = item.RatePerSession
                });
            }
        }

        await _context.SaveChangesAsync();
        return Json(new
        {
            success = true,
            message = "Đã lưu bảng đơn giá chung. Nếu giáo viên vẫn chưa có lương, hãy kiểm tra bậc lương của giáo viên ở danh sách bên dưới."
        });
    }

    private static List<PayRateConfigItemViewModel> GenerateDefaultMatrix()
    {
        var list = new List<PayRateConfigItemViewModel>();
        for (var tier = 1; tier <= 5; tier++)
        {
            for (var diff = 1; diff <= 3; diff++)
            {
                list.Add(new PayRateConfigItemViewModel
                {
                    Id = 0,
                    TeacherTier = tier,
                    CourseDifficulty = diff,
                    RatePerSession = 200000 + (tier * 50000) + (diff * 20000)
                });
            }
        }

        return list;
    }

    private static string GetDifficultyLabel(int difficultyLevel) => difficultyLevel switch
    {
        1 => "Môn cơ bản",
        2 => "Môn nâng cao",
        3 => "Môn chuyên sâu",
        _ => "Chưa phân loại"
    };
}

public class PayRateConfigIndexViewModel
{
    public List<PayRateConfigItemViewModel> Configs { get; set; } = new();
    public IReadOnlyList<TeacherSalaryAssignmentViewModel> Teachers { get; set; } = [];
    public IReadOnlyList<CourseDifficultyAssignmentViewModel> Courses { get; set; } = [];

    public int TotalTeachers => Teachers.Count;
    public int TeachersWithCustomRate => Teachers.Count(x => x.CustomSessionRate.HasValue && x.CustomSessionRate.Value > 0);
    public int TeachersWithTier => Teachers.Count(x => x.HasTeacherProfile && !x.CustomSessionRate.HasValue && x.SalaryTier.HasValue);
    public int TeachersMissingSetup => Teachers.Count(x => !x.HasTeacherProfile);
    public int CoursesConfigured => Courses.Count(x => x.DifficultyLevel is >= 1 and <= 3);
    public int CoursesMissingDifficulty => Courses.Count(x => x.DifficultyLevel is < 1 or > 3);
}

public class TeacherSalaryAssignmentViewModel
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int? SalaryTier { get; set; }
    public decimal? CustomSessionRate { get; set; }
    public bool HasTeacherProfile { get; set; }
}

public class CourseDifficultyAssignmentViewModel
{
    public int CourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public string DifficultyLabel { get; set; } = string.Empty;
    public int TotalClasses { get; set; }
}

public class PayRateConfigItemViewModel
{
    public int Id { get; set; }

    [Required]
    [Range(1, 10)]
    public int TeacherTier { get; set; }

    [Required]
    [Range(1, 3)]
    public int CourseDifficulty { get; set; }

    [Required]
    [Range(0, 10000000)]
    public decimal RatePerSession { get; set; }
}
