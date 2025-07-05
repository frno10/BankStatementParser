using System;
using System.Threading.Tasks;

namespace BankStatementParsing.CLI.Services;

public interface IExtractionService
{
    Task<ExtractionResult> ExtractTextAsync(string pdfPath, string? outputDir = null, bool force = false, string? account = null);
}

public class ExtractionResult
{
    public string InputFile { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; }
}