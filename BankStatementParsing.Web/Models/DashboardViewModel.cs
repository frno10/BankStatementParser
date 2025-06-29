using BankStatementParsing.Infrastructure;

namespace BankStatementParsing.Web.Models;

public class DashboardViewModel
{
    public int TotalAccounts { get; set; }
    public int TotalStatements { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal NetBalance { get; set; }
    
    public List<AccountSummary> AccountSummaries { get; set; } = new();
    public List<MonthlyTransactionSummary> MonthlyTrends { get; set; } = new();
    public List<TopMerchant> TopMerchants { get; set; } = new();
    public List<TransactionCategorySummary> CategoryBreakdown { get; set; } = new();
    public List<Transaction> RecentTransactions { get; set; } = new();
}

public class AccountSummary
{
    public int AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal NetBalance { get; set; }
    public DateTime? LastTransactionDate { get; set; }
}

public class MonthlyTransactionSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal NetAmount { get; set; }
}

public class TopMerchant
{
    public int MerchantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class TransactionCategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
} 