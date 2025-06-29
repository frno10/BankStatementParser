using BankStatementParsing.Infrastructure;
using BankStatementParsing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BankStatementParsing.Web.Controllers;

public class DashboardController : Controller
{
    private readonly BankStatementParsingContext _context;

    public DashboardController(BankStatementParsingContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel();

        // Basic counts
        model.TotalAccounts = await _context.Accounts.CountAsync();
        model.TotalStatements = await _context.Statements.CountAsync();
        model.TotalTransactions = await _context.Transactions.CountAsync();

        // Financial summary
        var transactions = await _context.Transactions.ToListAsync();
        model.TotalCredits = (decimal)transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        model.TotalDebits = Math.Abs((decimal)transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));
        model.NetBalance = model.TotalCredits - model.TotalDebits;

        // Account summaries
        model.AccountSummaries = await GetAccountSummaries();

        // Monthly trends (last 12 months)
        model.MonthlyTrends = await GetMonthlyTrends();

        // Top merchants by transaction volume
        model.TopMerchants = await GetTopMerchants();

        // Category breakdown (using merchant tags)
        model.CategoryBreakdown = await GetCategoryBreakdown();

        // Recent transactions (last 20)
        model.RecentTransactions = await _context.Transactions
            .Include(t => t.Statement)
                .ThenInclude(s => s.Account)
            .Include(t => t.Merchant)
            .OrderByDescending(t => t.Date)
            .Take(20)
            .ToListAsync();

        return View(model);
    }

    private async Task<List<AccountSummary>> GetAccountSummaries()
    {
        var accounts = await _context.Accounts
            .Include(a => a.Statements)
                .ThenInclude(s => s.Transactions)
            .ToListAsync();

        return accounts.Select(a => new AccountSummary
        {
            AccountId = a.Id,
            AccountNumber = a.AccountNumber,
            BankName = a.Name ?? "Unknown Bank",
            Currency = a.Currency ?? "Unknown",
            TransactionCount = a.Statements.SelectMany(s => s.Transactions).Count(),
            TotalCredits = (decimal)a.Statements.SelectMany(s => s.Transactions)
                .Where(t => t.Amount > 0).Sum(t => t.Amount),
            TotalDebits = Math.Abs((decimal)a.Statements.SelectMany(s => s.Transactions)
                .Where(t => t.Amount < 0).Sum(t => t.Amount)),
            NetBalance = (decimal)a.Statements.SelectMany(s => s.Transactions).Sum(t => t.Amount),
            LastTransactionDate = a.Statements.SelectMany(s => s.Transactions)
                .Max(t => (DateTime?)t.Date)
        }).ToList();
    }

    private async Task<List<MonthlyTransactionSummary>> GetMonthlyTrends()
    {
        var twelveMonthsAgo = DateTime.Now.AddMonths(-12);
        
        var monthlyData = await _context.Transactions
            .Where(t => t.Date >= twelveMonthsAgo)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new MonthlyTransactionSummary
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                TransactionCount = g.Count(),
                TotalCredits = (decimal)g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                TotalDebits = (decimal)g.Where(t => t.Amount < 0).Sum(t => t.Amount),
                NetAmount = (decimal)g.Sum(t => t.Amount)
            })
            .OrderBy(m => m.Year)
            .ThenBy(m => m.Month)
            .ToListAsync();

        // Apply Math.Abs on client side
        foreach (var item in monthlyData)
        {
            item.TotalDebits = Math.Abs(item.TotalDebits);
        }

        return monthlyData ?? new List<MonthlyTransactionSummary>();
    }

    private async Task<List<TopMerchant>> GetTopMerchants()
    {
        var merchants = await _context.Merchants
            .Include(m => m.MerchantTags)
                .ThenInclude(mt => mt.Tag)
            .Include(m => m.Transactions)
            .ToListAsync();

        return merchants
            .Select(m => new TopMerchant
            {
                MerchantId = m.Id,
                MerchantName = m.Name,
                TransactionCount = m.Transactions.Count(),
                TotalAmount = Math.Abs((decimal)m.Transactions.Sum(t => t.Amount)),
                Tags = m.MerchantTags.Select(mt => mt.Tag.Name).ToList()
            })
            .OrderByDescending(m => m.TotalAmount)
            .Take(10)
            .ToList();
    }

    private async Task<List<TransactionCategorySummary>> GetCategoryBreakdown()
    {
        // Get total amount using client-side evaluation
        var debitTransactions = await _context.Transactions
            .Where(t => t.Amount < 0)
            .ToListAsync();
        
        var totalAmount = debitTransactions.Sum(t => Math.Abs(t.Amount));

        if (totalAmount == 0) return new List<TransactionCategorySummary>();

        // Get categories with transactions using client-side evaluation
        var tags = await _context.Tags
            .Include(t => t.MerchantTags)
                .ThenInclude(mt => mt.Merchant)
                    .ThenInclude(m => m.Transactions)
            .ToListAsync();

        var categories = tags
            .Select(t => new TransactionCategorySummary
            {
                Category = t.Name,
                TransactionCount = t.MerchantTags
                    .SelectMany(mt => mt.Merchant.Transactions)
                    .Where(tx => tx.Amount < 0)
                    .Count(),
                TotalAmount = Math.Abs((decimal)t.MerchantTags
                    .SelectMany(mt => mt.Merchant.Transactions)
                    .Where(tx => tx.Amount < 0)
                    .Sum(tx => tx.Amount)),
                Percentage = 0 // Will calculate below
            })
            .Where(c => c.TotalAmount > 0)
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        // Calculate percentages
        foreach (var category in categories)
        {
            category.Percentage = Math.Round((category.TotalAmount / (decimal)totalAmount) * 100, 2);
        }

        return categories;
    }
} 