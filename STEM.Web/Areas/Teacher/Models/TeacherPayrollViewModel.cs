namespace STEM.Web.Areas.Teacher.Models;

public class TeacherPayrollIndexViewModel
{
    public string TeacherName { get; set; } = string.Empty;
    public int SalaryTier { get; set; }
    public string SalaryTierLabel { get; set; } = string.Empty;
    public bool HasSalaryTierConfigured { get; set; }

    /// <summary>Ước tính lương tháng hiện tại (real-time từ session thực tế).</summary>
    public TeacherPayrollEstimateViewModel CurrentMonthEstimate { get; set; } = new();

    /// <summary>Danh sách các kỳ lương đã được Admin sinh ra.</summary>
    public IReadOnlyList<TeacherPayrollHistoryItemViewModel> History { get; set; } = [];
}

/// <summary>
/// Ước tính lương tháng hiện tại, tính trực tiếp từ Session mà không cần Admin Generate trước.
/// </summary>
public class TeacherPayrollEstimateViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    public int TotalSessions { get; set; }
    public int ValidSessions { get; set; }
    public int PendingSessions { get; set; }
    public int InvalidSessions { get; set; }

    public decimal EstimatedSessionEarnings { get; set; }
    public decimal EstimatedBonuses { get; set; }
    public decimal EstimatedDeductions { get; set; }
    public decimal EstimatedTotalPay { get; set; }

    public int ValidPercent => TotalSessions > 0
        ? (int)Math.Round((double)ValidSessions * 100 / TotalSessions)
        : 0;

    public bool HasData => TotalSessions > 0;

    /// <summary>Đã có PayrollRecord (Admin đã Generate) hay chưa.</summary>
    public bool HasOfficialRecord { get; set; }

    /// <summary>Trạng thái PayrollRecord nếu đã có (Draft / Approved).</summary>
    public string? OfficialRecordStatus { get; set; }
}

/// <summary>
/// 1 dòng trong danh sách lịch sử lương (đã được Admin Generate).
/// </summary>
public class TeacherPayrollHistoryItemViewModel
{
    public int RecordId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public int TotalValidSessions { get; set; }
    public decimal SessionEarnings { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal TotalPay { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

/// <summary>
/// Chi tiết 1 kỳ lương cụ thể (từ PayrollRecord đã được Admin Generate).
/// </summary>
public class TeacherPayrollPeriodViewModel
{
    public int RecordId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int SalaryTier { get; set; }
    public string SalaryTierLabel { get; set; } = string.Empty;

    // KPIs
    public int TotalSessions { get; set; }
    public int ValidSessions { get; set; }
    public int InvalidSessions { get; set; }
    public int PendingSessions { get; set; }
    public decimal SessionEarnings { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal TotalPay { get; set; }

    // Trạng thái
    public string Status { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Breakdown thưởng / phạt
    public IReadOnlyList<TeacherPayrollBreakdownItem> BonusItems { get; set; } = [];
    public IReadOnlyList<TeacherPayrollBreakdownItem> DeductionItems { get; set; } = [];

    // Danh sách session trong kỳ
    public IReadOnlyList<TeacherPayrollSessionRowViewModel> Sessions { get; set; } = [];

    public int ValidPercent => TotalSessions > 0
        ? (int)Math.Round((double)ValidSessions * 100 / TotalSessions)
        : 0;
}

/// <summary>
/// 1 dòng thưởng hoặc phạt trong breakdown lương.
/// </summary>
public class TeacherPayrollBreakdownItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Icon { get; set; } = string.Empty;
    public bool IsBonus { get; set; }
}

/// <summary>
/// 1 dòng session trong bảng chi tiết kỳ lương.
/// </summary>
public class TeacherPayrollSessionRowViewModel
{
    public int SessionId { get; set; }
    public string SessionLabel { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int CourseDifficulty { get; set; }
    public string CourseDifficultyLabel { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string PayrollStatus { get; set; } = string.Empty;
    public string PayrollStatusLabel { get; set; } = string.Empty;
    public string PayrollStatusBadgeClass { get; set; } = string.Empty;
    public decimal RateForSession { get; set; }
}
