using BankStatementParsing.Core.Enums;

namespace BankStatementParsing.Core.Models;

public class TransactionData
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string? Category { get; set; }
    public string? Reference { get; set; }
    public decimal Balance { get; set; }
    public string? Counterparty { get; set; }
    public string? Purpose { get; set; }
} 