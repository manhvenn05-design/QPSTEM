using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace STEM.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class FinanceController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
