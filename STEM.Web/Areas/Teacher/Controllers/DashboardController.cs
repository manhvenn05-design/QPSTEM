using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STEM.Web.Areas.Teacher.Controllers;

[Area("Teacher")]
[Authorize(Roles = "Teacher")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Bảng điều khiển";
        return View();
    }
}
