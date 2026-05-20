namespace STEM.Web.Models;

public partial class PayrollRecord
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public int TotalValidSessions { get; set; }

    public decimal SessionEarnings { get; set; }

    public decimal Bonuses { get; set; }

    public decimal Deductions { get; set; }

    public decimal TotalPay { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? AdjustmentNotes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public virtual User Teacher { get; set; } = null!;
}
