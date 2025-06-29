namespace BankStatementParsing.Core.Models;

public class BankStatementData
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public DateTime StatementPeriodStart { get; set; }
    public DateTime StatementPeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string Currency { get; set; } = "RUB";
    public List<TransactionData> Transactions { get; set; } = new();
} 