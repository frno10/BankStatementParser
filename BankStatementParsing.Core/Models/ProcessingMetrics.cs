namespace BankStatementParsing.Core.Models;

public class ProcessingMetrics
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Operation { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int FilesProcessed { get; set; }
    public int TransactionsProcessed { get; set; }
    public int ErrorsCount { get; set; }
    public long MemoryUsedBytes { get; set; }
    public double CpuUsagePercent { get; set; }
    public string? Details { get; set; } // JSON details
    public bool Success { get; set; } = true;
}