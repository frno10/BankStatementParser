using Microsoft.AspNetCore.Mvc;

namespace BankStatementParsing.Web.Controllers;

public class SettingsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Rules()
    {
        return View();
    }

    public IActionResult Notifications()
    {
        return View();
    }

    public IActionResult Export()
    {
        return View();
    }

    public IActionResult Schedule()
    {
        return View();
    }

    public IActionResult General()
    {
        return View();
    }
}