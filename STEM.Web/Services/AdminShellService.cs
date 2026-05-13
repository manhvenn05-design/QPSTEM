using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models.Layout;

namespace STEM.Web.Services;

public sealed class AdminShellService : IAdminShellService
{
    private readonly ApplicationDbContext _context;

    public AdminShellService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminShellState> BuildAsync(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = int.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : (int?)null;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var profile = userId.HasValue
            ? await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == userId.Value)
                .Select(x => new { x.FullName, x.AvatarUrl })
                .FirstOrDefaultAsync()
            : null;

        var todaySessionCount = await _context.Sessions.AsNoTracking().CountAsync(x => x.Date == today);
        var openMaintenanceCount = await _context.MaintenanceLogs.AsNoTracking().CountAsync(x => x.Status == 1 || x.Status == 2);
        var unpaidInvoiceCount = await _context.Invoices.AsNoTracking().CountAsync(x => !x.Payments.Any() || x.Payments.Sum(p => (decimal?)p.Amount) < x.FinalAmount);
        var pendingLeadsCount = await _context.Leads.AsNoTracking().CountAsync(x => x.Status == 0);

        return new AdminShellState
        {
            UserId = userId,
            DisplayName = profile?.FullName ?? user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? "Quản trị viên",
            AvatarUrl = profile?.AvatarUrl,
            TodaySessionCount = todaySessionCount,
            OpenMaintenanceCount = openMaintenanceCount,
            UnpaidInvoiceCount = unpaidInvoiceCount,
            PendingLeadsCount = pendingLeadsCount
        };
    }
}
