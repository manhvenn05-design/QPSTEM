using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Models.Layout;

namespace STEM.Web.Services;

public sealed class TeacherShellService : ITeacherShellService
{
    private readonly ApplicationDbContext _context;

    public TeacherShellService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TeacherShellState> BuildAsync(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = int.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : (int?)null;

        if (!userId.HasValue)
        {
            return new TeacherShellState
            {
                DisplayName = user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? "Giáo viên"
            };
        }

        var profile = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => new { x.FullName, x.AvatarUrl })
            .FirstOrDefaultAsync();

        return new TeacherShellState
        {
            UserId = userId,
            DisplayName = profile?.FullName ?? user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? "Giáo viên",
            AvatarUrl = profile?.AvatarUrl
        };
    }
}
