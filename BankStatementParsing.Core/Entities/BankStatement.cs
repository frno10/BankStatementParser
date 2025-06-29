using BankStatementParsing.Core.Enums;

namespace BankStatementParsing.Core.Entities;

public class BankStatement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime StatementPeriodStart { get; set; }
    public DateTime StatementPeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public ProcessingStatus Status { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? AccountHolderName { get; set; }
    public string? Currency { get; set; } = "RUB";
    public List<Transaction> Transactions { get; set; } = new();
} 