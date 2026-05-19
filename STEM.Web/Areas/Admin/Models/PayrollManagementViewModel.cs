namespace STEM.Web.Areas.Admin.Models;

public class PayrollManagementViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public int TotalTeachers { get; set; }
    public int ApprovedTeachers { get; set; }
    public decimal TotalPayout { get; set; }
    public decimal DraftPayout { get; set; }
    public IReadOnlyList<PayrollRecordItemViewModel> Records { get; set; } = [];
}

public class PayrollRecordItemViewModel
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherUsername { get; set; } = string.Empty;
    public string SalaryTier { get; set; } = string.Empty;
    public int TotalValidSessions { get; set; }
    public decimal SessionEarnings { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal TotalPay { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ApprovedAt { get; set; }
}
