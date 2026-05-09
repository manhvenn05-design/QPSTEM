using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;

    public LeadsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Admin/Leads
    public async Task<IActionResult> Index()
    {
        var leads = await _context.Leads
            .Include(l => l.Interested)
            .OrderByDescending(l => l.Id) // Hiện người đăng ký mới nhất lên đầu
            .ToListAsync();

        return View(leads);
    }

    // POST: Admin/Leads/UpdateStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, byte status)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
        {
            return NotFound();
        }

        lead.Status = status;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Cập nhật trạng thái thành công.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Leads/Convert/5
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _context.Leads.Include(l => l.Interested).FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
        {
            return NotFound();
        }

        // Lấy danh sách lớp học thuộc khóa học mà học sinh quan tâm (nếu có)
        var classesQuery = _context.Classes.AsQueryable();
        if (lead.InterestedId.HasValue)
        {
            classesQuery = classesQuery.Where(c => c.CourseId == lead.InterestedId.Value);
        }

        var classes = await classesQuery.Select(c => new
        {
            c.Id,
            Name = $"{c.ClassCode} ({(c.Course != null ? c.Course.Name : "")})"
        }).ToListAsync();

        ViewBag.Classes = new SelectList(classes, "Id", "Name");

        var model = new ConvertLeadViewModel
        {
            LeadId = lead.Id,
            ParentName = lead.ParentName,
            Phone = lead.Phone,
            Email = lead.Email,
            Username = string.IsNullOrWhiteSpace(lead.Phone) ? "hs_" : $"hs_{lead.Phone}"
        };

        return View(model);
    }

    // POST: Admin/Leads/Convert
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(ConvertLeadViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId);
            var classesQuery = _context.Classes.AsQueryable();
            if (lead?.InterestedId != null)
            {
                classesQuery = classesQuery.Where(c => c.CourseId == lead.InterestedId.Value);
            }
            var classes = await classesQuery.Select(c => new { c.Id, Name = $"{c.ClassCode} ({(c.Course != null ? c.Course.Name : "")})" }).ToListAsync();
            ViewBag.Classes = new SelectList(classes, "Id", "Name", model.SelectedClassId);
            return View(model);
        }

        var leadEntity = await _context.Leads.FindAsync(model.LeadId);
        if (leadEntity == null)
        {
            return NotFound();
        }

        // 1. Kiểm tra Username tồn tại
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == model.Username.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại.");
            // Nạp lại danh sách lớp
            var classes = await _context.Classes.Select(c => new { c.Id, Name = $"{c.ClassCode} ({(c.Course != null ? c.Course.Name : "")})" }).ToListAsync();
            ViewBag.Classes = new SelectList(classes, "Id", "Name", model.SelectedClassId);
            return View(model);
        }

        // Lấy RoleId của "Học sinh" (thường là RoleName = "Học sinh" hoặc "Student")
        // Giả sử RoleId = 3 cho Học sinh theo Database design
        var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Student" || r.Name == "Học sinh");
        if (studentRole == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy vai trò Học sinh trong hệ thống.";
            return RedirectToAction(nameof(Index));
        }

        // 2. Tạo User
        var user = new User
        {
            FullName = model.StudentName,
            Username = model.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Email = model.Email,
            Phone = model.Phone,
            RoleId = studentRole.Id,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 3. Tạo StudentProfile (nếu có bảng StudentProfile - tùy thuộc Database hiện tại có không)
        // Nếu database thiết kế User có bảng con StudentProfile:
        var studentProfile = new StudentProfile
        {
            UserId = user.Id,
            GuardianName = model.ParentName ?? string.Empty,
            GuardianPhone = model.Phone ?? string.Empty
        };
        _context.StudentProfiles.Add(studentProfile);

        // 4. Đăng ký lớp (Enrollment) nếu có chọn
        if (model.SelectedClassId.HasValue)
        {
            var enrollment = new Enrollment
            {
                StudentId = user.Id,
                ClassId = model.SelectedClassId.Value,
                EnrollDate = DateTime.Now
            };
            _context.Enrollments.Add(enrollment);
        }

        // 5. Cập nhật Status của Lead thành Đã Chốt (Status = 2)
        leadEntity.Status = 2;
        
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Tạo tài khoản học viên {model.StudentName} thành công!";
        return RedirectToAction(nameof(Index));
    }
}
