namespace BankStatementParsing.CLI.Services;

public interface IPipelineService
{
    Task<ProcessResult> ProcessFileAsync(ProcessRequest request);
}

public class ProcessRequest
{
    public string InputFile { get; set; } = string.Empty;
    public string Bank { get; set; } = string.Empty;
    public string? Account { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool SkipDuplicates { get; set; } = true;
    public bool DryRun { get; set; } = false;
}

public class ProcessResult
{
    public string InputFile { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? FailedStep { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int TransactionCount { get; set; }
    
    public StepResult? ExtractStep { get; set; }
    public StepResult? ParseStep { get; set; }
    public StepResult? ImportStep { get; set; }
}

public class StepResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public string? OutputPath { get; set; }
}