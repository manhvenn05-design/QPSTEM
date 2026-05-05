using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace STEM.Web.Areas.Admin.Models;

public class FinanceManagementViewModel
{
    public string SelectedFilter { get; set; } = "all";
    public string SearchTerm { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int UnpaidInvoiceCount { get; set; }
    public int PaymentCount { get; set; }
    public IReadOnlyList<FinanceFilterViewModel> Filters { get; set; } = [];
    public IReadOnlyList<InvoiceManagementItemViewModel> Invoices { get; set; } = [];
    public IReadOnlyList<PaymentManagementItemViewModel> Payments { get; set; } = [];
}

public class FinanceFilterViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class InvoiceManagementItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string ClassCode { get; set; } = "Chưa gắn lớp";
    public decimal FinalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public string FinalAmountText { get; set; } = string.Empty;
    public string PaidAmountText { get; set; } = string.Empty;
    public string DueAmountText { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
}

public class PaymentManagementItemViewModel
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountText { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime TransDate { get; set; }
    public string TransDateText { get; set; } = string.Empty;
}

public class CreateInvoiceViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn học viên.")]
    [Display(Name = "Học viên")]
    public int? StudentId { get; set; }

    [Display(Name = "Lớp học")]
    public int? ClassId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số hóa đơn.")]
    [StringLength(50, ErrorMessage = "Số hóa đơn tối đa 50 ký tự.")]
    [Display(Name = "Số hóa đơn")]
    public string InvoiceNo { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "Số tiền phải lớn hơn 0.")]
    [Display(Name = "Tổng tiền")]
    public decimal FinalAmount { get; set; }

    public IReadOnlyList<SelectListItem> StudentOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> ClassOptions { get; set; } = [];
}

public class EditInvoiceViewModel : CreateInvoiceViewModel
{
    public int Id { get; set; }
}

public class InvoiceDetailsViewModel
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentUsername { get; set; } = string.Empty;
    public string? StudentEmail { get; set; }
    public string ClassCode { get; set; } = "Chưa gắn lớp";
    public decimal FinalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public string FinalAmountText { get; set; } = string.Empty;
    public string PaidAmountText { get; set; } = string.Empty;
    public string DueAmountText { get; set; } = string.Empty;
    public byte Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public IReadOnlyList<PaymentManagementItemViewModel> Payments { get; set; } = [];
}

public class CreatePaymentViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn hóa đơn.")]
    [Display(Name = "Hóa đơn")]
    public int? InvoiceId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "Số tiền phải lớn hơn 0.")]
    [Display(Name = "Số tiền thanh toán")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phương thức.")]
    [StringLength(20, ErrorMessage = "Phương thức tối đa 20 ký tự.")]
    [Display(Name = "Phương thức")]
    public string PaymentMethod { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn thời gian thanh toán.")]
    [Display(Name = "Thời gian thanh toán")]
    public DateTime? TransDate { get; set; }

    public decimal InvoiceFinalAmount { get; set; }
    public decimal InvoicePaidAmount { get; set; }
    public decimal InvoiceDueAmount { get; set; }
    public IReadOnlyList<SelectListItem> InvoiceOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> PaymentMethodOptions { get; set; } = [];
}

public class EditPaymentViewModel : CreatePaymentViewModel
{
    public int Id { get; set; }
}

public class PaymentDetailsViewModel
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = "Chưa gắn lớp";
    public decimal Amount { get; set; }
    public string AmountText { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime TransDate { get; set; }
    public string TransDateText { get; set; } = string.Empty;
}
