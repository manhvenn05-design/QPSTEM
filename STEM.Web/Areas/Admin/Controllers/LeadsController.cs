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
        var leadEntity = await _context.Leads.FirstOrDefaultAsync(x => x.Id == model.LeadId);

        if (leadEntity == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateClassOptionsAsync(leadEntity.InterestedId, model.SelectedClassId);
            return View(model);
        }

        // 1. Kiểm tra Username tồn tại
        if (model.SelectedClassId.HasValue && !await IsValidTargetClassAsync(leadEntity, model.SelectedClassId.Value))
        {
            ModelState.AddModelError(nameof(model.SelectedClassId), "Lớp được chọn không còn phù hợp với lead này.");
            await PopulateClassOptionsAsync(leadEntity.InterestedId, model.SelectedClassId);
            return View(model);
        }

        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == model.Username.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Username), "Tên đăng nhập đã tồn tại.");
            // Nạp lại danh sách lớp
            await PopulateClassOptionsAsync(leadEntity.InterestedId, model.SelectedClassId);
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

    private async Task PopulateClassOptionsAsync(int? interestedCourseId, int? selectedClassId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var classesQuery = _context.Classes
            .AsNoTracking()
            .Where(c => !c.EndDate.HasValue || c.EndDate.Value >= today);

        if (interestedCourseId.HasValue)
        {
            classesQuery = classesQuery.Where(c => c.CourseId == interestedCourseId.Value);
        }

        var classes = await classesQuery
            .Select(c => new
            {
                c.Id,
                Name = $"{c.ClassCode} ({(c.Course != null ? c.Course.Name : "")})"
            })
            .ToListAsync();

        ViewBag.Classes = new SelectList(classes, "Id", "Name", selectedClassId);
    }

    private async Task<bool> IsValidTargetClassAsync(Lead lead, int classId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await _context.Classes.AnyAsync(c =>
            c.Id == classId &&
            (!lead.InterestedId.HasValue || c.CourseId == lead.InterestedId.Value) &&
            (!c.EndDate.HasValue || c.EndDate.Value >= today));
    }

    /// <summary>
    /// Xóa lead. Lead không có dữ liệu con nên có thể xóa trực tiếp.
    /// Lead đã được chuyển đổi (Status=2) không nên xóa để giữ lịch sử.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads.FirstOrDefaultAsync(x => x.Id == id);

        if (lead == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khách tư vấn.";
            return RedirectToAction(nameof(Index));
        }

        if (lead.Status == 2)
        {
            TempData["ErrorMessage"] = $"Không thể xóa \"{lead.ParentName}\" vì đã được chuyển đổi thành học viên. Dữ liệu này thuộc lịch sử tuyển sinh.";
            return RedirectToAction(nameof(Index));
        }

        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã xóa khách tư vấn \"{lead.ParentName}\"";
        return RedirectToAction(nameof(Index));
    }
}
