namespace STEM.Web.Models.Layout;

public sealed class AdminShellState
{
    public int? UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int TodaySessionCount { get; init; }
    public int OpenMaintenanceCount { get; init; }
    public int UnpaidInvoiceCount { get; init; }
    public int PendingLeadsCount { get; init; }
    public int NotificationCount => TodaySessionCount + OpenMaintenanceCount + UnpaidInvoiceCount + PendingLeadsCount;
}
