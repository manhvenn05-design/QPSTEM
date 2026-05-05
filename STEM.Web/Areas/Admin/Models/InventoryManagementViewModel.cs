using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class InventoryManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public int? SelectedCategoryId { get; set; }
    public int TotalCategories { get; set; }
    public int TotalEquipments { get; set; }
    public int ReadyEquipments { get; set; }
    public int ActiveBorrows { get; set; }
    public int OpenMaintenanceLogs { get; set; }
    public IReadOnlyList<InventoryFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<InventoryCategorySummaryViewModel> Categories { get; set; } = [];
    public IReadOnlyList<InventoryEquipmentItemViewModel> Equipments { get; set; } = [];
    public IReadOnlyList<InventoryMaintenanceItemViewModel> MaintenanceLogs { get; set; } = [];
}

public class InventoryFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class InventoryCategorySummaryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int ReadyCount { get; set; }
    public int BorrowedCount { get; set; }
    public int MaintenanceCount { get; set; }
}

public class InventoryEquipmentItemViewModel
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public bool HasActiveBorrow { get; set; }
    public string BorrowSummary { get; set; } = string.Empty;
    public int MaintenanceCount { get; set; }
    public string LastIssueSummary { get; set; } = string.Empty;
}

public class InventoryMaintenanceItemViewModel
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentSerialNumber { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class CreateEquipmentCategoryViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
    [StringLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự.")]
    [Display(Name = "Tên danh mục")]
    public string Name { get; set; } = string.Empty;
}

public class EditEquipmentCategoryViewModel : CreateEquipmentCategoryViewModel
{
    public int Id { get; set; }
}

public class CreateEquipmentViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
    [Display(Name = "Danh mục")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã thiết bị.")]
    [StringLength(50, ErrorMessage = "Mã thiết bị tối đa 50 ký tự.")]
    [Display(Name = "Mã thiết bị")]
    public string SerialNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [Display(Name = "Trạng thái")]
    public byte? Status { get; set; }

    public string? ImageUrl { get; set; }

    [Display(Name = "Ảnh đại diện")]
    public IFormFile? ImageFile { get; set; }

    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];
}

public class EditEquipmentViewModel : CreateEquipmentViewModel
{
    public int Id { get; set; }
    public string? CurrentImageUrl { get; set; }
}

public class EquipmentDetailsViewModel
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public bool HasActiveBorrow { get; set; }
    public InventoryBorrowSummaryViewModel? ActiveBorrow { get; set; }
    public IReadOnlyList<InventoryBorrowSummaryViewModel> BorrowHistory { get; set; } = [];
    public IReadOnlyList<InventoryMaintenanceSummaryViewModel> MaintenanceLogs { get; set; } = [];
}

public class InventoryBorrowSummaryViewModel
{
    public int Id { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string BorrowerName { get; set; } = string.Empty;
    public string BorrowTimeText { get; set; } = string.Empty;
    public string ReturnTimeText { get; set; } = string.Empty;
    public bool IsReturned { get; set; }
}

public class InventoryMaintenanceSummaryViewModel
{
    public int Id { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class CreateMaintenanceLogViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn thiết bị.")]
    [Display(Name = "Thiết bị")]
    public int? EquipmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn người báo.")]
    [Display(Name = "Người báo")]
    public int? ReportedBy { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mô tả sự cố.")]
    [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    [Display(Name = "Mô tả sự cố")]
    public string Issue { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [Display(Name = "Trạng thái")]
    public byte? Status { get; set; }

    public IReadOnlyList<SelectListItem> EquipmentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ReporterOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = [];
}

public class EditMaintenanceLogViewModel : CreateMaintenanceLogViewModel
{
    public int Id { get; set; }
}

public class MaintenanceLogDetailsViewModel
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentSerialNumber { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string ReporterName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class CreateEquipmentBorrowViewModel
{
    public int EquipmentId { get; set; }
    public string EquipmentSerialNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn buổi học.")]
    [Display(Name = "Buổi học")]
    public int? SessionId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn người mượn.")]
    [Display(Name = "Người mượn")]
    public int? BorrowerId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian mượn.")]
    [Display(Name = "Thời gian mượn")]
    public DateTime? BorrowTime { get; set; }

    public IReadOnlyList<SelectListItem> SessionOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> BorrowerOptions { get; set; } = [];
}
