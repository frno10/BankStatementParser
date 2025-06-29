using BankStatementParsing.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BankStatementParsing.Web.Models;

public class TransactionFilterViewModel
{
    // Filter criteria
    public int? AccountId { get; set; }
    public int? MerchantId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public string? Description { get; set; }
    public string? Currency { get; set; }
    public bool? IsCredit { get; set; } // null = all, true = credits only, false = debits only
    public List<int> TagIds { get; set; } = new();
    
    // Sorting
    public string SortBy { get; set; } = "Date";
    public string SortDirection { get; set; } = "desc";
    
    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    
    // Results
    public List<TransactionViewModel> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    
    // Summary of filtered results
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal NetAmount { get; set; }
    
    // Dropdown options
    public List<SelectListItem> Accounts { get; set; } = new();
    public List<SelectListItem> Merchants { get; set; } = new();
    public List<SelectListItem> Tags { get; set; } = new();
    public List<SelectListItem> Currencies { get; set; } = new();
}

public class TransactionViewModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public bool IsCredit => Amount > 0;
    
    // Account info
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    
    // Merchant info
    public string? MerchantName { get; set; }
    public List<string> MerchantTags { get; set; } = new();
    
    // Transaction tags
    public List<string> TransactionTags { get; set; } = new();
    
    // Statement info
    public string? StatementNumber { get; set; }
} 