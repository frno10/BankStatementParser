using BankStatementParsing.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementParsing.Web.Controllers;

public class AnalyticsController : Controller
{
    private readonly BankStatementParsingContext _context;

    public AnalyticsController(BankStatementParsingContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult MonthlyReports()
    {
        // TODO: Implement monthly reports view
        return View();
    }

    public IActionResult CategoryAnalysis()
    {
        // TODO: Implement category analysis view
        return View();
    }

    public IActionResult MerchantAnalysis()
    {
        // TODO: Implement merchant analysis view
        return View();
    }
} 