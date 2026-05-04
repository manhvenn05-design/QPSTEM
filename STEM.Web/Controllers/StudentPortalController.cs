using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STEM.Web.Controllers;

[Authorize(Roles = "Student")]
public class StudentPortalController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Cổng học sinh";
        return View();
    }
}
