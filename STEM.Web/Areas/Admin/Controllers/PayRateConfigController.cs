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

    // GET: Admin/PayRateConfig
    public async Task<IActionResult> Index()
    {
        var configs = await _context.Set<PayRateConfig>()
            .AsNoTracking()
            .OrderBy(x => x.TeacherTier)
            .ThenBy(x => x.CourseDifficulty)
            .ToListAsync();

        var model = new PayRateConfigIndexViewModel
        {
            Configs = configs.Select(x => new PayRateConfigItemViewModel
            {
                Id = x.Id,
                TeacherTier = x.TeacherTier,
                CourseDifficulty = x.CourseDifficulty,
                RatePerSession = x.RatePerSession
            }).ToList()
        };

        // Đảm bảo đủ matrix 5x3 (nếu DB rỗng)
        if (model.Configs.Count == 0)
        {
            model.Configs = GenerateDefaultMatrix();
        }

        ViewData["Title"] = "Cấu hình đơn giá lương";
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
        return Json(new { success = true, message = "Đã lưu cấu hình bảng giá." });
    }

    private List<PayRateConfigItemViewModel> GenerateDefaultMatrix()
    {
        var list = new List<PayRateConfigItemViewModel>();
        for (int tier = 1; tier <= 5; tier++)
        {
            for (int diff = 1; diff <= 3; diff++)
            {
                list.Add(new PayRateConfigItemViewModel
                {
                    Id = 0,
                    TeacherTier = tier,
                    CourseDifficulty = diff,
                    RatePerSession = 200000 + (tier * 50000) + (diff * 20000) // Ví dụ base
                });
            }
        }
        return list;
    }
}

public class PayRateConfigIndexViewModel
{
    public List<PayRateConfigItemViewModel> Configs { get; set; } = new();
}

public class PayRateConfigItemViewModel
{
    public int Id { get; set; }
    
    [Required]
    [Range(1, 10)]
    public int TeacherTier { get; set; }
    
    [Required]
    [Range(1, 5)]
    public int CourseDifficulty { get; set; }
    
    [Required]
    [Range(0, 10000000)]
    public decimal RatePerSession { get; set; }
}
