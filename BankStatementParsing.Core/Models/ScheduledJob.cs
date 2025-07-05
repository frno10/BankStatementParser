namespace BankStatementParsing.Core.Models;

public class ScheduledJob
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty; // ProcessFiles, ExportData, etc.
    public string CronExpression { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Parameters { get; set; } // JSON parameters
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public string? LastRunResult { get; set; }
    public bool LastRunSuccessful { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}