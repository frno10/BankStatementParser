using BankStatementParsing.Infrastructure;
using BankStatementParsing.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BankStatementParsing.Web.Controllers;

public class TransactionsController : Controller
{
    private readonly BankStatementParsingContext _context;

    public TransactionsController(BankStatementParsingContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(TransactionFilterViewModel model)
    {
        // Load dropdown options
        await LoadDropdownOptions(model);

        // Build query
        var query = _context.Transactions
            .Include(t => t.Statement)
                .ThenInclude(s => s.Account)
            .Include(t => t.Merchant)
            .Include(t => t.TransactionTags)
                .ThenInclude(tt => tt.Tag)
            .AsQueryable();

        // Apply filters
        if (model.AccountId.HasValue)
            query = query.Where(t => t.Statement.AccountId == model.AccountId.Value);

        if (model.MerchantId.HasValue)
            query = query.Where(t => t.MerchantId == model.MerchantId.Value);

        if (model.DateFrom.HasValue)
            query = query.Where(t => t.Date >= model.DateFrom.Value);

        if (model.DateTo.HasValue)
            query = query.Where(t => t.Date <= model.DateTo.Value);

        if (model.AmountFrom.HasValue)
            query = query.Where(t => (t.Amount >= (double)model.AmountFrom.Value) || (t.Amount <= -(double)model.AmountFrom.Value));

        if (model.AmountTo.HasValue)
            query = query.Where(t => (t.Amount <= (double)model.AmountTo.Value && t.Amount >= 0) || (t.Amount >= -(double)model.AmountTo.Value && t.Amount < 0));

        if (!string.IsNullOrWhiteSpace(model.Description))
            query = query.Where(t => t.Description != null && t.Description.Contains(model.Description));

        if (!string.IsNullOrWhiteSpace(model.Currency))
            query = query.Where(t => t.Currency == model.Currency);

        if (model.IsCredit.HasValue)
        {
            if (model.IsCredit.Value)
                query = query.Where(t => t.Amount > 0);
            else
                query = query.Where(t => t.Amount < 0);
        }

        if (model.TagIds.Any())
        {
            query = query.Where(t => 
                t.TransactionTags.Any(tt => model.TagIds.Contains(tt.TagId)));
        }

        // Calculate summary before pagination
        var summaryQuery = query.Select(t => t.Amount);
        var allAmounts = await summaryQuery.ToListAsync();
        model.TotalCredits = (decimal)allAmounts.Where(a => a > 0).Sum();
        model.TotalDebits = Math.Abs((decimal)allAmounts.Where(a => a < 0).Sum());
        model.NetAmount = (decimal)allAmounts.Sum();

        // Apply sorting
        query = model.SortBy.ToLower() switch
        {
            "date" => model.SortDirection == "asc" ? query.OrderBy(t => t.Date) : query.OrderByDescending(t => t.Date),
            "amount" => model.SortDirection == "asc" ? query.OrderBy(t => t.Amount) : query.OrderByDescending(t => t.Amount),
            "description" => model.SortDirection == "asc" ? query.OrderBy(t => t.Description) : query.OrderByDescending(t => t.Description),
            "merchant" => model.SortDirection == "asc" ? query.OrderBy(t => t.Merchant!.Name) : query.OrderByDescending(t => t.Merchant!.Name),
            "account" => model.SortDirection == "asc" ? query.OrderBy(t => t.Statement.Account.AccountNumber) : query.OrderByDescending(t => t.Statement.Account.AccountNumber),
            _ => query.OrderByDescending(t => t.Date)
        };

        // Get total count for pagination
        model.TotalCount = await query.CountAsync();
        model.TotalPages = (int)Math.Ceiling((double)model.TotalCount / model.PageSize);

        // Apply pagination
        var transactions = await query
            .Skip((model.Page - 1) * model.PageSize)
            .Take(model.PageSize)
            .ToListAsync();

        // Load merchant tags separately for better performance
        var merchantIds = transactions.Where(t => t.MerchantId.HasValue).Select(t => t.MerchantId!.Value).Distinct().ToList();
        var merchantTags = await _context.MerchantTags
            .Include(mt => mt.Tag)
            .Where(mt => merchantIds.Contains(mt.MerchantId))
            .ToListAsync();

        // Map to view models
        model.Transactions = transactions.Select(t => new TransactionViewModel
        {
            Id = t.Id,
            Date = t.Date,
            Description = t.Description ?? "",
            Amount = (decimal)t.Amount,
            Currency = t.Currency ?? "",
            Reference = t.Reference,
            AccountNumber = t.Statement.Account.AccountNumber,
            BankName = t.Statement.Account.Name ?? "Unknown Bank",
            MerchantName = t.Merchant?.Name,
            MerchantTags = t.MerchantId.HasValue 
                ? merchantTags.Where(mt => mt.MerchantId == t.MerchantId).Select(mt => mt.Tag.Name).ToList() 
                : new(),
            TransactionTags = t.TransactionTags.Select(tt => tt.Tag.Name).ToList(),
            StatementNumber = t.Statement.StatementNumber
        }).ToList();

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Statement)
                .ThenInclude(s => s.Account)
            .Include(t => t.Merchant)
            .Include(t => t.TransactionTags)
                .ThenInclude(tt => tt.Tag)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaction == null)
        {
            return NotFound();
        }

        var viewModel = new TransactionViewModel
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Description = transaction.Description ?? "",
            Amount = (decimal)transaction.Amount,
            Currency = transaction.Currency ?? "",
            Reference = transaction.Reference,
            AccountNumber = transaction.Statement.Account.AccountNumber,
            BankName = transaction.Statement.Account.Name ?? "Unknown Bank",
            MerchantName = transaction.Merchant?.Name,
            TransactionTags = transaction.TransactionTags.Select(tt => tt.Tag.Name).ToList(),
            StatementNumber = transaction.Statement.StatementNumber
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Export(TransactionFilterViewModel model)
    {
        // Similar filtering logic as Index but return CSV
        var query = _context.Transactions
            .Include(t => t.Statement)
                .ThenInclude(s => s.Account)
            .Include(t => t.Merchant)
            .AsQueryable();

        // Apply same filters as Index method
        // ... (filtering code would be extracted to a shared method in real implementation)

        var transactions = await query.ToListAsync();
        
        var csv = GenerateCsv(transactions);
        var fileName = $"transactions_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private async Task LoadDropdownOptions(TransactionFilterViewModel model)
    {
        // Load accounts
        model.Accounts = await _context.Accounts
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.AccountNumber} - {a.Name}"
            })
            .ToListAsync();

        // Load merchants
        model.Merchants = await _context.Merchants
            .Select(m => new SelectListItem
            {
                Value = m.Id.ToString(),
                Text = m.Name
            })
            .ToListAsync();

        // Load tags
        model.Tags = await _context.Tags
            .Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.Name
            })
            .ToListAsync();

        // Load currencies
        model.Currencies = await _context.Transactions
            .Where(t => !string.IsNullOrEmpty(t.Currency))
            .Select(t => t.Currency!)
            .Distinct()
            .Select(c => new SelectListItem
            {
                Value = c,
                Text = c
            })
            .ToListAsync();
    }

    private string GenerateCsv(List<BankStatementParsing.Infrastructure.Transaction> transactions)
    {
        var lines = new List<string>
        {
            "Date,Description,Amount,Currency,Reference,Account,Bank,Merchant"
        };

        foreach (var t in transactions)
        {
            lines.Add($"{t.Date:yyyy-MM-dd},{EscapeCsv(t.Description)},{t.Amount},{t.Currency},{EscapeCsv(t.Reference)},{t.Statement?.Account?.AccountNumber},{EscapeCsv(t.Statement?.Account?.Name)},{EscapeCsv(t.Merchant?.Name)}");
        }

        return string.Join("\n", lines);
    }

    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
} 