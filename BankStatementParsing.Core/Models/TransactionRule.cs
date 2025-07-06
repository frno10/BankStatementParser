namespace BankStatementParsing.Core.Models;

public class TransactionRule
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 1;
    
    // Rule conditions
    public string? DescriptionContains { get; set; }
    public string? DescriptionRegex { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public decimal? AmountEquals { get; set; }
    public string? MerchantName { get; set; }
    public string? Reference { get; set; }
    public string? TransactionType { get; set; }
    
    // Rule actions
    public string? AssignCategory { get; set; }
    public string? AssignTags { get; set; } // Comma-separated tags
    public string? AssignMerchant { get; set; }
    public string? SetNote { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}