using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Teacher.Models;
using STEM.Web.Data;
using STEM.Web.Models;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class EquipmentController : Controller
{
    private readonly ApplicationDbContext _context;

    public EquipmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var availableEquipments = await _context.Equipments
            .AsNoTracking()
            .Where(x => x.Status == 1 && !x.EquipmentBorrows.Any(b => b.ReturnTime == null))
            .OrderBy(x => x.Category.Name)
            .ThenBy(x => x.SerialNumber)
            .Select(x => new TeacherEquipmentAvailableItemViewModel
            {
                Id = x.Id,
                SerialNumber = x.SerialNumber,
                CategoryName = x.Category.Name,
                ImageUrl = x.ImageUrl,
                StatusLabel = "Sẵn sàng",
                StatusBadgeClass = "teacher-tag teacher-tag--success"
            })
            .ToListAsync();

        var borrowRows = await _context.EquipmentBorrows
            .AsNoTracking()
            .Where(x => x.BorrowerId == teacherId.Value)
            .OrderByDescending(x => x.BorrowTime)
            .Select(x => new TeacherEquipmentBorrowItemViewModel
            {
                BorrowId = x.Id,
                EquipmentId = x.EquipmentId,
                SerialNumber = x.Equipment.SerialNumber,
                CategoryName = x.Equipment.Category.Name,
                ImageUrl = x.Equipment.ImageUrl,
                SessionLabel = $"Buổi số {x.Session.SessionNo:00} · {x.Session.Class.ClassCode}",
                BorrowTimeText = x.BorrowTime.ToString("dd/MM/yyyy HH:mm"),
                ReturnTimeText = x.ReturnTime.HasValue ? x.ReturnTime.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa trả",
                IsReturned = x.ReturnTime.HasValue
            })
            .ToListAsync();

        var model = new TeacherEquipmentIndexViewModel
        {
            AvailableCount = availableEquipments.Count,
            ActiveBorrowCount = borrowRows.Count(x => !x.IsReturned),
            ReturnedCount = borrowRows.Count(x => x.IsReturned),
            AvailableEquipments = availableEquipments,
            ActiveBorrows = borrowRows.Where(x => !x.IsReturned).ToList(),
            History = borrowRows.Where(x => x.IsReturned).Take(10).ToList()
        };

        ViewData["Title"] = "Mượn / Trả thiết bị";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Borrow(int? equipmentId = null, int? sessionId = null)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var model = new TeacherBorrowEquipmentViewModel
        {
            EquipmentId = equipmentId,
            SelectedEquipmentId = equipmentId,
            SessionId = sessionId,
            BorrowTime = DateTime.Now
        };

        await PopulateOptionsAsync(model, teacherId.Value);
        ViewData["Title"] = "Mượn / Trả thiết bị";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrow(TeacherBorrowEquipmentViewModel model)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        await PopulateOptionsAsync(model, teacherId.Value);

        if (!await IsTeacherSessionAsync(model.SessionId, teacherId.Value))
        {
            ModelState.AddModelError(nameof(model.SessionId), "Vui lòng chọn buổi học thuộc lịch dạy của bạn.");
        }

        if (!model.SelectedEquipmentId.HasValue || !await _context.Equipments.AnyAsync(x => x.Id == model.SelectedEquipmentId.Value))
        {
            ModelState.AddModelError(nameof(model.SelectedEquipmentId), "Vui lòng chọn thiết bị hợp lệ.");
        }

        if (model.SelectedEquipmentId.HasValue)
        {
            var equipment = await _context.Equipments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.SelectedEquipmentId.Value);

            if (equipment == null || equipment.Status != 1)
            {
                ModelState.AddModelError(nameof(model.SelectedEquipmentId), "Thiết bị này hiện chưa sẵn sàng để mượn.");
            }

            if (await _context.EquipmentBorrows.AnyAsync(x => x.EquipmentId == model.SelectedEquipmentId.Value && x.ReturnTime == null))
            {
                ModelState.AddModelError(nameof(model.SelectedEquipmentId), "Thiết bị này đang có phiếu mượn mở.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Mượn / Trả thiết bị";
            return View(model);
        }

        var entity = new EquipmentBorrow
        {
            EquipmentId = model.SelectedEquipmentId!.Value,
            SessionId = model.SessionId!.Value,
            BorrowerId = teacherId.Value,
            BorrowTime = model.BorrowTime!.Value
        };

        _context.EquipmentBorrows.Add(entity);

        var trackedEquipment = await _context.Equipments.FirstAsync(x => x.Id == model.SelectedEquipmentId.Value);
        trackedEquipment.Status = 2;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã ghi nhận phiếu mượn thiết bị.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(int id)
    {
        var teacherId = GetCurrentTeacherId();
        if (!teacherId.HasValue)
        {
            return Challenge();
        }

        var entity = await _context.EquipmentBorrows
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.Id == id && x.BorrowerId == teacherId.Value);

        if (entity == null)
        {
            return NotFound();
        }

        if (!entity.ReturnTime.HasValue)
        {
            entity.ReturnTime = DateTime.Now;
            await _context.SaveChangesAsync();
            await UpdateEquipmentStatusAsync(entity.EquipmentId);
        }

        TempData["SuccessMessage"] = "Đã xác nhận trả thiết bị.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(TeacherBorrowEquipmentViewModel model, int teacherId)
    {
        model.EquipmentOptions = await _context.Equipments
            .AsNoTracking()
            .Where(x => x.Status == 1 && !x.EquipmentBorrows.Any(b => b.ReturnTime == null))
            .OrderBy(x => x.Category.Name)
            .ThenBy(x => x.SerialNumber)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.SerialNumber} · {x.Category.Name}"
            })
            .ToListAsync();

        model.SessionOptions = await _context.Sessions
            .AsNoTracking()
            .Where(x => x.Class.TeacherId == teacherId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.StartTime)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.Class.ClassCode} · Buổi số {x.SessionNo:00} · {x.Date:dd/MM/yyyy}"
            })
            .ToListAsync();
    }

    private async Task<bool> IsTeacherSessionAsync(int? sessionId, int teacherId)
    {
        return sessionId.HasValue &&
               await _context.Sessions.AnyAsync(x => x.Id == sessionId.Value && x.Class.TeacherId == teacherId);
    }

    private async Task UpdateEquipmentStatusAsync(int equipmentId)
    {
        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.Id == equipmentId);
        if (equipment == null || equipment.Status == 4)
        {
            return;
        }

        var hasActiveBorrow = await _context.EquipmentBorrows.AnyAsync(x => x.EquipmentId == equipmentId && x.ReturnTime == null);
        var hasOpenMaintenance = await _context.MaintenanceLogs.AnyAsync(x => x.EquipmentId == equipmentId && (x.Status == 1 || x.Status == 2));

        equipment.Status = hasOpenMaintenance
            ? (byte)3
            : hasActiveBorrow
                ? (byte)2
                : (byte)1;

        await _context.SaveChangesAsync();
    }

    private int? GetCurrentTeacherId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userId, out var teacherId) ? teacherId : null;
    }
}
