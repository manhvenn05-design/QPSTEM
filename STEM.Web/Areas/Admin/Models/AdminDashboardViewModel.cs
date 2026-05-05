namespace STEM.Web.Areas.Admin.Models;

public class AdminDashboardViewModel
{
    public string TodayLabel { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int ActiveClassCount { get; set; }
    public int TodaySessionCount { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int OpenMaintenanceCount { get; set; }
    public int PublishedPostCount { get; set; }
    public int ActiveBannerCount { get; set; }
    public int UpcomingClassCount { get; set; }
    public int AttendanceCompletionPercent { get; set; }
    public IReadOnlyList<DashboardMetricViewModel> Metrics { get; set; } = [];
    public IReadOnlyList<DashboardRevenuePointViewModel> RevenueSeries { get; set; } = [];
    public IReadOnlyList<DashboardTodaySessionViewModel> TodaySessions { get; set; } = [];
    public IReadOnlyList<DashboardUpcomingClassViewModel> UpcomingClasses { get; set; } = [];
    public IReadOnlyList<DashboardInvoiceAlertViewModel> InvoiceAlerts { get; set; } = [];
    public IReadOnlyList<DashboardMaintenanceAlertViewModel> MaintenanceAlerts { get; set; } = [];
}

public class DashboardMetricViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class DashboardRevenuePointViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountText { get; set; } = string.Empty;
    public int HeightPercent { get; set; }
}

public class DashboardTodaySessionViewModel
{
    public int Id { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string TimeRange { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
    public int AttendanceCount { get; set; }
    public int AttendancePercent { get; set; }
}

public class DashboardUpcomingClassViewModel
{
    public int Id { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string StartDateText { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
}

public class DashboardInvoiceAlertViewModel
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string DueAmountText { get; set; } = string.Empty;
}

public class DashboardMaintenanceAlertViewModel
{
    public int Id { get; set; }
    public string EquipmentSerialNumber { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
}
