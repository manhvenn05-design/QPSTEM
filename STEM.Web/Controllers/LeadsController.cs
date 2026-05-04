using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Controllers;

public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;

    public LeadsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Consultation(int? courseId)
    {
        // Lấy danh sách khóa học để đưa vào Dropdown
        ViewBag.Courses = await _context.Courses.ToListAsync();
        
        var model = new LeadViewModel();
        if (courseId.HasValue)
        {
            model = new LeadViewModel { InterestedId = courseId.Value };
        }
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Consultation(LeadViewModel model)
    {
        if (!ModelState.IsValid)
        {
        ViewBag.Courses = await _context.Courses.ToListAsync();
            return View(model);
        }

        // Lưu vào bảng Leads
        var newLead = new Lead
        {
            ParentName = model.ParentName!,
            Phone = model.Phone!,
            InterestedId = model.InterestedId,
            Status = 0 // 0: Chưa liên hệ (Mặc định)
        };

        _context.Leads.Add(newLead);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Gửi yêu cầu thành công! Chúng tôi sẽ liên hệ với bạn trong thời gian sớm nhất.";
        return RedirectToAction("Index", "Home");
    }
}
