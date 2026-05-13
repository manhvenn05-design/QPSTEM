namespace STEM.Web.Models.StudentViewModels;

public class StudentFinancePageViewModel
{
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining => TotalBilled - TotalPaid;
    public int PendingCount { get; set; }
    public List<StudentInvoiceDetailViewModel> Invoices { get; set; } = new();
}

public class StudentInvoiceDetailViewModel
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string? ClassCode { get; set; }
    public string? CourseName { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount => FinalAmount - PaidAmount;
    public DateTime? DueDate { get; set; }

    // Đồng bộ FinanceController: 1=Chưa thu, 2=Một phần, 3=Đã thu đủ
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;

    public int ProgressPercent => FinalAmount > 0
        ? (int)Math.Min(100, Math.Round(PaidAmount / FinalAmount * 100))
        : 100;

    public bool IsOverdue =>
        RemainingAmount > 0 &&
        DueDate.HasValue &&
        DueDate.Value.Date < DateTime.Today;

    public List<StudentPaymentItemViewModel> Payments { get; set; } = new();
}

public class StudentPaymentItemViewModel
{
    public decimal Amount { get; set; }
    public string PaymentMethodLabel { get; set; } = string.Empty;
    public DateTime TransDate { get; set; }
}
