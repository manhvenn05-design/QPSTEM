using System.Security.Claims;
using STEM.Web.Models.Layout;

namespace STEM.Web.Services;

public interface ITeacherShellService
{
    Task<TeacherShellState> BuildAsync(ClaimsPrincipal user);
}
