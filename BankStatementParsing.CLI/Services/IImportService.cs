using System;
using System.Threading.Tasks;

namespace BankStatementParsing.CLI.Services;

public interface IImportService
{
    Task<ImportResult> ImportFileAsync(string dataPath, string? account = null, bool skipDuplicates = true, bool dryRun = false);
}

public class ImportResult
{
    public string InputFile { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int TotalCount { get; set; }
    public string? Account { get; set; }
}