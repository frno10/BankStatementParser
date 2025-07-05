namespace BankStatementParsing.Core.Models;

public class NotificationSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public string? EmailAddress { get; set; }
    public bool SmsEnabled { get; set; } = false;
    public string? PhoneNumber { get; set; }
    public bool NotifyOnProcessingComplete { get; set; } = true;
    public bool NotifyOnProcessingFailed { get; set; } = true;
    public bool NotifyOnLargeTransactions { get; set; } = false;
    public decimal LargeTransactionThreshold { get; set; } = 1000;
    public bool NotifyOnDuplicateTransactions { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}