namespace BankStatementParsing.Core.Models;

public class ExportRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Format { get; set; } = string.Empty; // CSV, Excel, QIF, OFX, JSON
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? AccountFilter { get; set; }
    public string? CategoryFilter { get; set; }
    public string? MerchantFilter { get; set; }
    public string? TagFilter { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public string? FilePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}