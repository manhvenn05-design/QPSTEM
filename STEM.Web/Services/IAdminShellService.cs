using System.Security.Claims;
using STEM.Web.Models.Layout;

namespace STEM.Web.Services;

public interface IAdminShellService
{
    Task<AdminShellState> BuildAsync(ClaimsPrincipal user);
}
