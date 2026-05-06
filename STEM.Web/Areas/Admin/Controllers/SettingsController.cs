using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new SettingsViewModel
        {
            TotalUsers = await _context.Users.CountAsync(),
            ActiveTeachers = await _context.Users.CountAsync(x => x.Role.Name == "Teacher" && x.IsActive),
            ActiveClasses = await _context.Classes.CountAsync(x => x.StartDate <= DateOnly.FromDateTime(DateTime.Today) &&
                                                                    (!x.EndDate.HasValue || x.EndDate >= DateOnly.FromDateTime(DateTime.Today))),
            UnpaidInvoices = await _context.Invoices.CountAsync(x => x.FinalAmount > (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m)),
            EquipmentsInMaintenance = await _context.MaintenanceLogs.CountAsync(x => x.Status == 1 || x.Status == 2),
            PublishedPosts = await _context.Posts.CountAsync(x => x.IsPublished),
            ActiveBanners = await _context.Banners.CountAsync(x => x.IsActive)
        };

        return View(model);
    }
}
