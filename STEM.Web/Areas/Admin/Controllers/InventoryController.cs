using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Areas.Admin.Models;
using STEM.Web.Data;
using STEM.Web.Models;
using STEM.Web.Services;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public InventoryController(ApplicationDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all", string? q = null, int? categoryId = null)
    {
        var filters = new[]
        {
            new InventoryFilterViewModel { Key = "all", Label = "Tất cả" },
            new InventoryFilterViewModel { Key = "ready", Label = "Sẵn sàng" },
            new InventoryFilterViewModel { Key = "borrowed", Label = "Đang mượn" },
            new InventoryFilterViewModel { Key = "maintenance", Label = "Bảo trì" },
            new InventoryFilterViewModel { Key = "inactive", Label = "Ngưng dùng" }
        };

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLowerInvariant();
        if (filters.All(x => x.Key != normalizedFilter))
        {
            normalizedFilter = "all";
        }

        var searchTerm = q?.Trim() ?? string.Empty;

        var categories = await _context.EquipmentCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new InventoryCategorySummaryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                TotalQuantity = x.Equipment.Count,
                ReadyCount = x.Equipment.Count(e => e.Status == 1),
                BorrowedCount = x.Equipment.Count(e => e.Status == 2),
                MaintenanceCount = x.Equipment.Count(e => e.Status == 3)
            })
            .ToListAsync();

        var equipmentsQuery = _context.Equipments
            .AsNoTracking()
            .Select(x => new EquipmentListProjection
            {
                Id = x.Id,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.Name,
                SerialNumber = x.SerialNumber,
                ImageUrl = x.ImageUrl,
                Status = x.Status,
                ActiveBorrowId = x.EquipmentBorrows
                    .Where(b => b.ReturnTime == null)
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => (int?)b.Id)
                    .FirstOrDefault(),
                ActiveBorrowerName = x.EquipmentBorrows
                    .Where(b => b.ReturnTime == null)
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => b.Borrower.FullName)
                    .FirstOrDefault(),
                ActiveSessionNo = x.EquipmentBorrows
                    .Where(b => b.ReturnTime == null)
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => (int?)b.Session.SessionNo)
                    .FirstOrDefault(),
                ActiveClassCode = x.EquipmentBorrows
                    .Where(b => b.ReturnTime == null)
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => b.Session.Class.ClassCode)
                    .FirstOrDefault(),
                MaintenanceCount = x.MaintenanceLogs.Count,
                LatestIssue = x.MaintenanceLogs
                    .OrderByDescending(m => m.Id)
                    .Select(m => m.Issue)
                    .FirstOrDefault()
            });

        if (categoryId.HasValue)
        {
            equipmentsQuery = equipmentsQuery.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            equipmentsQuery = equipmentsQuery.Where(x =>
                x.SerialNumber.Contains(searchTerm) ||
                x.CategoryName.Contains(searchTerm));
        }

        equipmentsQuery = ApplyEquipmentFilter(equipmentsQuery, normalizedFilter);

        var equipments = await equipmentsQuery
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.SerialNumber)
            .ToListAsync();

        var maintenanceQuery = _context.MaintenanceLogs
            .AsNoTracking()
            .Select(x => new InventoryMaintenanceItemViewModel
            {
                Id = x.Id,
                EquipmentId = x.EquipmentId,
                EquipmentSerialNumber = x.Equipment.SerialNumber,
                ReporterName = x.ReportedByNavigation.FullName,
                Issue = x.Issue,
                Status = x.Status
            });

        if (categoryId.HasValue)
        {
            maintenanceQuery = maintenanceQuery.Where(x => _context.Equipments
                .Where(e => e.Id == x.EquipmentId)
                .Select(e => e.CategoryId)
                .FirstOrDefault() == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            maintenanceQuery = maintenanceQuery.Where(x =>
                x.EquipmentSerialNumber.Contains(searchTerm) ||
                x.ReporterName.Contains(searchTerm) ||
                x.Issue.Contains(searchTerm));
        }

        var maintenanceLogs = await maintenanceQuery
            .OrderByDescending(x => x.Id)
            .Take(12)
            .ToListAsync();

        foreach (var item in maintenanceLogs)
        {
            ApplyMaintenanceStatus(item);
        }

        var model = new InventoryManagementViewModel
        {
            SelectedFilter = normalizedFilter,
            SearchTerm = searchTerm,
            SelectedCategoryId = categoryId,
            TotalCategories = categories.Count,
            TotalEquipments = await _context.Equipments.CountAsync(),
            ReadyEquipments = await _context.Equipments.CountAsync(x => x.Status == 1),
            ActiveBorrows = await _context.EquipmentBorrows.CountAsync(x => x.ReturnTime == null),
            OpenMaintenanceLogs = await _context.MaintenanceLogs.CountAsync(x => x.Status == 1 || x.Status == 2),
            Filters = filters,
            Categories = categories,
            Equipments = equipments.Select(MapEquipmentItem).ToList(),
            MaintenanceLogs = maintenanceLogs
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EquipmentDetails(int id)
    {
        var model = await _context.Equipments
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new EquipmentDetailsViewModel
            {
                Id = x.Id,
                SerialNumber = x.SerialNumber,
                ImageUrl = x.ImageUrl,
                CategoryId = x.CategoryId,
                CategoryName = x.Category.Name,
                Status = x.Status,
                HasActiveBorrow = x.EquipmentBorrows.Any(b => b.ReturnTime == null),
                ActiveBorrow = x.EquipmentBorrows
                    .Where(b => b.ReturnTime == null)
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => new InventoryBorrowSummaryViewModel
                    {
                        Id = b.Id,
                        SessionLabel = $"Buổi {b.Session.SessionNo} - {b.Session.Class.ClassCode}",
                        BorrowerName = b.Borrower.FullName,
                        BorrowTimeText = b.BorrowTime.ToString("dd/MM/yyyy HH:mm"),
                        ReturnTimeText = "Chưa trả",
                        IsReturned = false
                    })
                    .FirstOrDefault(),
                BorrowHistory = x.EquipmentBorrows
                    .OrderByDescending(b => b.BorrowTime)
                    .Select(b => new InventoryBorrowSummaryViewModel
                    {
                        Id = b.Id,
                        SessionLabel = $"Buổi {b.Session.SessionNo} - {b.Session.Class.ClassCode}",
                        BorrowerName = b.Borrower.FullName,
                        BorrowTimeText = b.BorrowTime.ToString("dd/MM/yyyy HH:mm"),
                        ReturnTimeText = b.ReturnTime.HasValue ? b.ReturnTime.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa trả",
                        IsReturned = b.ReturnTime.HasValue
                    })
                    .ToList(),
                MaintenanceLogs = x.MaintenanceLogs
                    .OrderByDescending(m => m.Id)
                    .Select(m => new InventoryMaintenanceSummaryViewModel
                    {
                        Id = m.Id,
                        ReporterName = m.ReportedByNavigation.FullName,
                        Issue = m.Issue,
                        StatusLabel = string.Empty,
                        StatusBadgeClass = string.Empty
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        ApplyEquipmentStatus(model);

        var statuses = await _context.MaintenanceLogs
            .AsNoTracking()
            .Where(x => x.EquipmentId == id)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Status)
            .ToListAsync();

        for (var i = 0; i < model.MaintenanceLogs.Count; i++)
        {
            ApplyMaintenanceStatus(model.MaintenanceLogs[i], statuses[i]);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult CreateCategory()
    {
        return View(new CreateEquipmentCategoryViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CreateEquipmentCategoryViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new EquipmentCategory
        {
            Name = model.Name.Trim(),
            TotalQuantity = 0
        };

        _context.EquipmentCategories.Add(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã thêm danh mục thiết bị.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var entity = await _context.EquipmentCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        return View(new EditEquipmentCategoryViewModel
        {
            Id = entity.Id,
            Name = entity.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(EditEquipmentCategoryViewModel model)
    {
        var entity = await _context.EquipmentCategories.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entity.Name = model.Name.Trim();
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã cập nhật danh mục thiết bị.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var entity = await _context.EquipmentCategories
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        if (entity.Equipment.Count > 0)
        {
            TempData["ErrorMessage"] = "Không thể xóa danh mục đang có thiết bị.";
            return RedirectToAction(nameof(Index));
        }

        _context.EquipmentCategories.Remove(entity);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa danh mục thiết bị.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateEquipment()
    {
        var model = new CreateEquipmentViewModel();
        await PopulateEquipmentOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEquipment(CreateEquipmentViewModel model)
    {
        await PopulateEquipmentOptionsAsync(model);
        var normalizedSerial = model.SerialNumber.Trim().ToUpperInvariant();

        if (await _context.Equipments.AnyAsync(x => x.SerialNumber.ToLower() == normalizedSerial.ToLower()))
        {
            ModelState.AddModelError(nameof(model.SerialNumber), "Mã thiết bị đã tồn tại.");
        }

        if (!await IsValidCategoryAsync(model.CategoryId))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new Equipment
        {
            CategoryId = model.CategoryId!.Value,
            SerialNumber = normalizedSerial,
            Status = model.Status!.Value
        };

        string? uploadedImageUrl = null;
        try
        {
            if (model.ImageFile != null)
            {
                uploadedImageUrl = await _fileStorage.SaveFileAsync(model.ImageFile, "equipments");
                entity.ImageUrl = uploadedImageUrl;
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
            return View(model);
        }

        try
        {
            _context.Equipments.Add(entity);
            await _context.SaveChangesAsync();
            await SyncCategoryTotalsAsync(entity.CategoryId);
        }
        catch (DbUpdateException ex) when (IsDuplicateSerialNumber(ex))
        {
            if (uploadedImageUrl != null)
            {
                await _fileStorage.DeleteFileAsync(uploadedImageUrl);
            }

            ModelState.AddModelError(nameof(model.SerialNumber), "Mã thiết bị đã tồn tại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã thêm thiết bị mới.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditEquipment(int id)
    {
        var entity = await _context.Equipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditEquipmentViewModel
        {
            Id = entity.Id,
            CategoryId = entity.CategoryId,
            SerialNumber = entity.SerialNumber,
            Status = entity.Status,
            ImageUrl = entity.ImageUrl,
            CurrentImageUrl = entity.ImageUrl
        };

        await PopulateEquipmentOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEquipment(EditEquipmentViewModel model)
    {
        await PopulateEquipmentOptionsAsync(model);
        var entity = await _context.Equipments.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        var normalizedSerial = model.SerialNumber.Trim().ToUpperInvariant();
        if (await _context.Equipments.AnyAsync(x => x.Id != model.Id && x.SerialNumber.ToLower() == normalizedSerial.ToLower()))
        {
            ModelState.AddModelError(nameof(model.SerialNumber), "Mã thiết bị đã tồn tại.");
        }

        if (!await IsValidCategoryAsync(model.CategoryId))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            model.CurrentImageUrl = entity.ImageUrl;
            return View(model);
        }

        var oldCategoryId = entity.CategoryId;
        var oldImageUrl = entity.ImageUrl;
        entity.CategoryId = model.CategoryId!.Value;
        entity.SerialNumber = normalizedSerial;
        entity.Status = model.Status!.Value;

        string? uploadedImageUrl = null;
        try
        {
            if (model.ImageFile != null)
            {
                uploadedImageUrl = await _fileStorage.SaveFileAsync(model.ImageFile, "equipments");
                entity.ImageUrl = uploadedImageUrl;
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.ImageFile), ex.Message);
            model.CurrentImageUrl = entity.ImageUrl;
            return View(model);
        }

        try
        {
            await _context.SaveChangesAsync();
            await SyncCategoryTotalsAsync(oldCategoryId, entity.CategoryId);
            if (uploadedImageUrl != null && !string.IsNullOrWhiteSpace(oldImageUrl))
            {
                await _fileStorage.DeleteFileAsync(oldImageUrl);
            }
        }
        catch (DbUpdateException ex) when (IsDuplicateSerialNumber(ex))
        {
            if (uploadedImageUrl != null)
            {
                await _fileStorage.DeleteFileAsync(uploadedImageUrl);
            }

            ModelState.AddModelError(nameof(model.SerialNumber), "Mã thiết bị đã tồn tại.");
            model.CurrentImageUrl = oldImageUrl;
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật thiết bị.";
        return RedirectToAction(nameof(EquipmentDetails), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEquipment(int id)
    {
        var entity = await _context.Equipments
            .Include(x => x.EquipmentBorrows)
            .Include(x => x.MaintenanceLogs)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        if (entity.EquipmentBorrows.Count > 0 || entity.MaintenanceLogs.Count > 0)
        {
            TempData["ErrorMessage"] = "Không thể xóa thiết bị đã có lịch sử mượn hoặc bảo trì.";
            return RedirectToAction(nameof(Index));
        }

        var categoryId = entity.CategoryId;
        var imageUrl = entity.ImageUrl;
        _context.Equipments.Remove(entity);
        await _context.SaveChangesAsync();
        await SyncCategoryTotalsAsync(categoryId);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            await _fileStorage.DeleteFileAsync(imageUrl);
        }

        TempData["SuccessMessage"] = "Đã xóa thiết bị.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateMaintenance(int? equipmentId = null)
    {
        var model = new CreateMaintenanceLogViewModel
        {
            EquipmentId = equipmentId
        };
        await PopulateMaintenanceOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMaintenance(CreateMaintenanceLogViewModel model)
    {
        await PopulateMaintenanceOptionsAsync(model);

        if (!await IsValidEquipmentAsync(model.EquipmentId))
        {
            ModelState.AddModelError(nameof(model.EquipmentId), "Thiết bị không hợp lệ.");
        }

        if (!await IsValidUserAsync(model.ReportedBy))
        {
            ModelState.AddModelError(nameof(model.ReportedBy), "Người báo không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new MaintenanceLog
        {
            EquipmentId = model.EquipmentId!.Value,
            ReportedBy = model.ReportedBy!.Value,
            Issue = model.Issue.Trim(),
            Status = model.Status!.Value
        };

        _context.MaintenanceLogs.Add(entity);
        await _context.SaveChangesAsync();
        await UpdateEquipmentStatusFromMaintenanceAsync(entity.EquipmentId);

        TempData["SuccessMessage"] = "Đã thêm nhật ký bảo trì.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditMaintenance(int id)
    {
        var entity = await _context.MaintenanceLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditMaintenanceLogViewModel
        {
            Id = entity.Id,
            EquipmentId = entity.EquipmentId,
            ReportedBy = entity.ReportedBy,
            Issue = entity.Issue,
            Status = entity.Status
        };

        await PopulateMaintenanceOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMaintenance(EditMaintenanceLogViewModel model)
    {
        await PopulateMaintenanceOptionsAsync(model);
        var entity = await _context.MaintenanceLogs.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        if (!await IsValidEquipmentAsync(model.EquipmentId))
        {
            ModelState.AddModelError(nameof(model.EquipmentId), "Thiết bị không hợp lệ.");
        }

        if (!await IsValidUserAsync(model.ReportedBy))
        {
            ModelState.AddModelError(nameof(model.ReportedBy), "Người báo không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var oldEquipmentId = entity.EquipmentId;
        entity.EquipmentId = model.EquipmentId!.Value;
        entity.ReportedBy = model.ReportedBy!.Value;
        entity.Issue = model.Issue.Trim();
        entity.Status = model.Status!.Value;

        await _context.SaveChangesAsync();
        await UpdateEquipmentStatusFromMaintenanceAsync(oldEquipmentId);
        if (oldEquipmentId != entity.EquipmentId)
        {
            await UpdateEquipmentStatusFromMaintenanceAsync(entity.EquipmentId);
        }

        TempData["SuccessMessage"] = "Đã cập nhật nhật ký bảo trì.";
        return RedirectToAction(nameof(MaintenanceDetails), new { id = model.Id });
    }

    [HttpGet]
    public async Task<IActionResult> MaintenanceDetails(int id)
    {
        var model = await _context.MaintenanceLogs
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new MaintenanceLogDetailsViewModel
            {
                Id = x.Id,
                EquipmentId = x.EquipmentId,
                EquipmentSerialNumber = x.Equipment.SerialNumber,
                CategoryName = x.Equipment.Category.Name,
                ReporterName = x.ReportedByNavigation.FullName,
                Issue = x.Issue,
                Status = x.Status
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return NotFound();
        }

        ApplyMaintenanceStatus(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMaintenance(int id)
    {
        var entity = await _context.MaintenanceLogs.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
        {
            return NotFound();
        }

        var equipmentId = entity.EquipmentId;
        _context.MaintenanceLogs.Remove(entity);
        await _context.SaveChangesAsync();
        await UpdateEquipmentStatusFromMaintenanceAsync(equipmentId);

        TempData["SuccessMessage"] = "Đã xóa nhật ký bảo trì.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateBorrow(int equipmentId)
    {
        var equipment = await _context.Equipments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == equipmentId);

        if (equipment == null)
        {
            return NotFound();
        }

        if (equipment.Status == 3 || equipment.Status == 4)
        {
            TempData["ErrorMessage"] = "Thiết bị này hiện chưa thể cho mượn.";
            return RedirectToAction(nameof(EquipmentDetails), new { id = equipmentId });
        }

        if (await _context.EquipmentBorrows.AnyAsync(x => x.EquipmentId == equipmentId && x.ReturnTime == null))
        {
            TempData["ErrorMessage"] = "Thiết bị này đang được mượn.";
            return RedirectToAction(nameof(EquipmentDetails), new { id = equipmentId });
        }

        var model = new CreateEquipmentBorrowViewModel
        {
            EquipmentId = equipment.Id,
            EquipmentSerialNumber = equipment.SerialNumber,
            BorrowTime = DateTime.Now
        };

        await PopulateBorrowOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBorrow(CreateEquipmentBorrowViewModel model)
    {
        await PopulateBorrowOptionsAsync(model);

        if (!await IsValidEquipmentAsync(model.EquipmentId))
        {
            return NotFound();
        }

        if (!await IsValidSessionAsync(model.SessionId))
        {
            ModelState.AddModelError(nameof(model.SessionId), "Buổi học không hợp lệ.");
        }

        if (!await IsValidUserAsync(model.BorrowerId))
        {
            ModelState.AddModelError(nameof(model.BorrowerId), "Người mượn không hợp lệ.");
        }

        if (await _context.EquipmentBorrows.AnyAsync(x => x.EquipmentId == model.EquipmentId && x.ReturnTime == null))
        {
            ModelState.AddModelError(string.Empty, "Thiết bị này đang được mượn.");
        }

        var equipment = await _context.Equipments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.EquipmentId);
        if (equipment != null && (equipment.Status == 3 || equipment.Status == 4))
        {
            ModelState.AddModelError(string.Empty, "Thiết bị này hiện chưa thể cho mượn.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = new EquipmentBorrow
        {
            EquipmentId = model.EquipmentId,
            SessionId = model.SessionId!.Value,
            BorrowerId = model.BorrowerId!.Value,
            BorrowTime = model.BorrowTime!.Value
        };

        _context.EquipmentBorrows.Add(entity);
        var trackedEquipment = await _context.Equipments.FirstAsync(x => x.Id == model.EquipmentId);
        trackedEquipment.Status = 2;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã ghi nhận phiếu mượn thiết bị.";
        return RedirectToAction(nameof(EquipmentDetails), new { id = model.EquipmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnBorrow(int id)
    {
        var entity = await _context.EquipmentBorrows
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null)
        {
            return NotFound();
        }

        entity.ReturnTime = DateTime.Now;
        await _context.SaveChangesAsync();
        await UpdateEquipmentStatusFromMaintenanceAsync(entity.EquipmentId);

        TempData["SuccessMessage"] = "Đã xác nhận thiết bị được trả.";
        return RedirectToAction(nameof(EquipmentDetails), new { id = entity.EquipmentId });
    }

    private IQueryable<EquipmentListProjection> ApplyEquipmentFilter(IQueryable<EquipmentListProjection> query, string filter)
    {
        return filter switch
        {
            "ready" => query.Where(x => x.Status == 1),
            "borrowed" => query.Where(x => x.Status == 2 || x.ActiveBorrowerName != null),
            "maintenance" => query.Where(x => x.Status == 3),
            "inactive" => query.Where(x => x.Status == 4),
            _ => query
        };
    }

    private InventoryEquipmentItemViewModel MapEquipmentItem(EquipmentListProjection item)
    {
        var model = new InventoryEquipmentItemViewModel
        {
            Id = item.Id,
            SerialNumber = item.SerialNumber,
            ImageUrl = item.ImageUrl,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            Status = item.Status,
            ActiveBorrowId = item.ActiveBorrowId,
            HasActiveBorrow = !string.IsNullOrWhiteSpace(item.ActiveBorrowerName),
            BorrowSummary = !string.IsNullOrWhiteSpace(item.ActiveBorrowerName)
                ? $"{item.ActiveBorrowerName} · {item.ActiveClassCode} · Buổi {item.ActiveSessionNo}"
                : "Chưa có phiếu mượn đang mở",
            MaintenanceCount = item.MaintenanceCount,
            LastIssueSummary = string.IsNullOrWhiteSpace(item.LatestIssue) ? "Chưa có sự cố" : item.LatestIssue
        };

        ApplyEquipmentStatus(model);
        return model;
    }

    private void ApplyEquipmentStatus(InventoryEquipmentItemViewModel model)
    {
        (model.StatusLabel, model.StatusBadgeClass) = GetEquipmentStatusDisplay(model.Status);
    }

    private void ApplyEquipmentStatus(EquipmentDetailsViewModel model)
    {
        (model.StatusLabel, model.StatusBadgeClass) = GetEquipmentStatusDisplay(model.Status);
    }

    private void ApplyMaintenanceStatus(InventoryMaintenanceItemViewModel model)
    {
        (model.StatusLabel, model.StatusBadgeClass) = GetMaintenanceStatusDisplay(model.Status);
    }

    private void ApplyMaintenanceStatus(InventoryMaintenanceSummaryViewModel model, byte status)
    {
        (model.StatusLabel, model.StatusBadgeClass) = GetMaintenanceStatusDisplay(status);
    }

    private void ApplyMaintenanceStatus(MaintenanceLogDetailsViewModel model)
    {
        (model.StatusLabel, model.StatusBadgeClass) = GetMaintenanceStatusDisplay(model.Status);
    }

    private static (string Label, string BadgeClass) GetEquipmentStatusDisplay(byte status)
    {
        return status switch
        {
            1 => ("Sẵn sàng", "bg-[#edf7e8] text-[#456c3f]"),
            2 => ("Đang mượn", "bg-[#fff4e8] text-[#9b682f]"),
            3 => ("Bảo trì", "bg-[#eeeee9] text-[#42493d]"),
            4 => ("Ngưng dùng", "bg-[#ffdad6] text-[#ba1a1a]"),
            _ => ($"Trạng thái {status}", "bg-[#eeeee9] text-[#42493d]")
        };
    }

    private static (string Label, string BadgeClass) GetMaintenanceStatusDisplay(byte status)
    {
        return status switch
        {
            1 => ("Mới ghi nhận", "bg-[#fff4e8] text-[#9b682f]"),
            2 => ("Đang xử lý", "bg-[#eeeee9] text-[#42493d]"),
            3 => ("Hoàn tất", "bg-[#edf7e8] text-[#456c3f]"),
            _ => ($"Trạng thái {status}", "bg-[#eeeee9] text-[#42493d]")
        };
    }

    private async Task PopulateEquipmentOptionsAsync(CreateEquipmentViewModel model)
    {
        model.CategoryOptions = await _context.EquipmentCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();

        model.StatusOptions = GetEquipmentStatusOptions();
    }

    private async Task PopulateMaintenanceOptionsAsync(CreateMaintenanceLogViewModel model)
    {
        model.EquipmentOptions = await _context.Equipments
            .AsNoTracking()
            .OrderBy(x => x.SerialNumber)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.SerialNumber} · {x.Category.Name}"
            })
            .ToListAsync();

        model.ReporterOptions = await _context.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.FullName} ({x.Username})"
            })
            .ToListAsync();

        model.StatusOptions = GetMaintenanceStatusOptions();
    }

    private async Task PopulateBorrowOptionsAsync(CreateEquipmentBorrowViewModel model)
    {
        model.SessionOptions = await _context.Sessions
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.SessionNo)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.Class.ClassCode} · Buổi {x.SessionNo} · {x.Date:dd/MM/yyyy}"
            })
            .ToListAsync();

        model.BorrowerOptions = await _context.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.FullName} ({x.Username})"
            })
            .ToListAsync();
    }

    private IReadOnlyList<SelectListItem> GetEquipmentStatusOptions()
    {
        return
        [
            new SelectListItem { Value = "1", Text = "Sẵn sàng" },
            new SelectListItem { Value = "2", Text = "Đang mượn" },
            new SelectListItem { Value = "3", Text = "Bảo trì" },
            new SelectListItem { Value = "4", Text = "Ngưng dùng" }
        ];
    }

    private IReadOnlyList<SelectListItem> GetMaintenanceStatusOptions()
    {
        return
        [
            new SelectListItem { Value = "1", Text = "Mới ghi nhận" },
            new SelectListItem { Value = "2", Text = "Đang xử lý" },
            new SelectListItem { Value = "3", Text = "Hoàn tất" }
        ];
    }

    private async Task<bool> IsValidCategoryAsync(int? categoryId)
    {
        return categoryId.HasValue && await _context.EquipmentCategories.AnyAsync(x => x.Id == categoryId.Value);
    }

    private async Task<bool> IsValidEquipmentAsync(int? equipmentId)
    {
        return equipmentId.HasValue && await _context.Equipments.AnyAsync(x => x.Id == equipmentId.Value);
    }

    private async Task<bool> IsValidUserAsync(int? userId)
    {
        return userId.HasValue && await _context.Users.AnyAsync(x => x.Id == userId.Value);
    }

    private async Task<bool> IsValidSessionAsync(int? sessionId)
    {
        return sessionId.HasValue && await _context.Sessions.AnyAsync(x => x.Id == sessionId.Value);
    }

    private async Task SyncCategoryTotalsAsync(params int[] categoryIds)
    {
        var distinctIds = categoryIds.Where(x => x > 0).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return;
        }

        var categories = await _context.EquipmentCategories
            .Include(x => x.Equipment)
            .Where(x => distinctIds.Contains(x.Id))
            .ToListAsync();

        foreach (var category in categories)
        {
            category.TotalQuantity = category.Equipment.Count;
        }

        await _context.SaveChangesAsync();
    }

    private async Task UpdateEquipmentStatusFromMaintenanceAsync(int equipmentId)
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

    private static bool IsDuplicateSerialNumber(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException &&
               sqlException.Message.Contains("UQ__Equipmen__048A0008", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EquipmentListProjection
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public byte Status { get; set; }
        public int? ActiveBorrowId { get; set; }
        public string? ActiveBorrowerName { get; set; }
        public int? ActiveSessionNo { get; set; }
        public string? ActiveClassCode { get; set; }
        public int MaintenanceCount { get; set; }
        public string? LatestIssue { get; set; }
    }
}
