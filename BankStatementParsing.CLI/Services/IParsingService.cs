using System;
using System.Threading.Tasks;

namespace BankStatementParsing.CLI.Services;

public interface IParsingService
{
    Task<ParseResult> ParseFileAsync(string textPath, string bankType, string? outputDir = null, string format = "json", string? account = null);
}

public class ParseResult
{
    public string InputFile { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public int TransactionCount { get; set; }
    public string BankType { get; set; } = string.Empty;
    public DateTime? StatementStartDate { get; set; }
    public DateTime? StatementEndDate { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
}