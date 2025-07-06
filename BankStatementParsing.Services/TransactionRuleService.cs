using BankStatementParsing.Core.Models;
using BankStatementParsing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BankStatementParsing.Services;

public interface ITransactionRuleService
{
    Task<List<TransactionRule>> GetRulesAsync(int userId);
    Task<TransactionRule> CreateRuleAsync(TransactionRule rule);
    Task<TransactionRule> UpdateRuleAsync(TransactionRule rule);
    Task DeleteRuleAsync(int ruleId);
    Task<bool> ApplyRulesToTransactionAsync(Transaction transaction, int userId);
    Task<int> ApplyRulesToAllTransactionsAsync(int userId);
}

public class TransactionRuleService : ITransactionRuleService
{
    private readonly BankStatementParsingContext _context;
    private readonly ILogger<TransactionRuleService> _logger;

    public TransactionRuleService(
        BankStatementParsingContext context,
        ILogger<TransactionRuleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TransactionRule>> GetRulesAsync(int userId)
    {
        return await _context.TransactionRules
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    public async Task<TransactionRule> CreateRuleAsync(TransactionRule rule)
    {
        _context.TransactionRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task<TransactionRule> UpdateRuleAsync(TransactionRule rule)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        _context.TransactionRules.Update(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    public async Task DeleteRuleAsync(int ruleId)
    {
        var rule = await _context.TransactionRules.FindAsync(ruleId);
        if (rule != null)
        {
            _context.TransactionRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ApplyRulesToTransactionAsync(Transaction transaction, int userId)
    {
        var rules = await GetRulesAsync(userId);
        var applied = false;

        foreach (var rule in rules.Where(r => r.IsActive))
        {
            if (await EvaluateRuleAsync(rule, transaction))
            {
                await ApplyRuleActionsAsync(rule, transaction);
                applied = true;
                
                // Only apply the first matching rule (highest priority)
                break;
            }
        }

        if (applied)
        {
            await _context.SaveChangesAsync();
        }

        return applied;
    }

    public async Task<int> ApplyRulesToAllTransactionsAsync(int userId)
    {
        var rules = await GetRulesAsync(userId);
        var appliedCount = 0;

        // Get all transactions for the user's accounts
        var transactions = await _context.Transactions
            .Include(t => t.Statement)
            .ThenInclude(s => s.Account)
            .Where(t => t.Statement.UserId == userId)
            .ToListAsync();

        foreach (var transaction in transactions)
        {
            foreach (var rule in rules.Where(r => r.IsActive))
            {
                if (await EvaluateRuleAsync(rule, transaction))
                {
                    await ApplyRuleActionsAsync(rule, transaction);
                    appliedCount++;
                    break; // Only apply first matching rule
                }
            }
        }

        if (appliedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return appliedCount;
    }

    private async Task<bool> EvaluateRuleAsync(TransactionRule rule, Transaction transaction)
    {
        // Check description contains
        if (!string.IsNullOrEmpty(rule.DescriptionContains))
        {
            if (transaction.Description?.Contains(rule.DescriptionContains, StringComparison.OrdinalIgnoreCase) != true)
                return false;
        }

        // Check description regex
        if (!string.IsNullOrEmpty(rule.DescriptionRegex))
        {
            try
            {
                var regex = new Regex(rule.DescriptionRegex, RegexOptions.IgnoreCase);
                if (!regex.IsMatch(transaction.Description ?? string.Empty))
                    return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid regex pattern in rule {RuleId}: {Pattern}", rule.Id, rule.DescriptionRegex);
                return false;
            }
        }

        // Check amount conditions
        if (rule.AmountEquals.HasValue && Math.Abs(transaction.Amount - (double)rule.AmountEquals.Value) > 0.01)
            return false;

        if (rule.AmountMin.HasValue && transaction.Amount < (double)rule.AmountMin.Value)
            return false;

        if (rule.AmountMax.HasValue && transaction.Amount > (double)rule.AmountMax.Value)
            return false;

        // Check merchant name
        if (!string.IsNullOrEmpty(rule.MerchantName))
        {
            if (transaction.Merchant?.Name?.Contains(rule.MerchantName, StringComparison.OrdinalIgnoreCase) != true)
                return false;
        }

        // Check reference
        if (!string.IsNullOrEmpty(rule.Reference))
        {
            if (transaction.Reference?.Contains(rule.Reference, StringComparison.OrdinalIgnoreCase) != true)
                return false;
        }

        return true;
    }

    private async Task ApplyRuleActionsAsync(TransactionRule rule, Transaction transaction)
    {
        // Apply category
        if (!string.IsNullOrEmpty(rule.AssignCategory))
        {
            // You might want to create a category field in Transaction or handle this differently
            // For now, we'll use the ExtraInfo field to store category
            var extraInfo = string.IsNullOrEmpty(transaction.ExtraInfo) ? "{}" : transaction.ExtraInfo;
            // This is a simplified approach - you might want to use a proper JSON serializer
            transaction.ExtraInfo = $"{{\"category\":\"{rule.AssignCategory}\"}}";
        }

        // Apply merchant
        if (!string.IsNullOrEmpty(rule.AssignMerchant))
        {
            var merchant = await _context.Merchants
                .FirstOrDefaultAsync(m => m.Name.Equals(rule.AssignMerchant, StringComparison.OrdinalIgnoreCase));

            if (merchant == null)
            {
                merchant = new Merchant { Name = rule.AssignMerchant };
                _context.Merchants.Add(merchant);
                await _context.SaveChangesAsync();
            }

            transaction.MerchantId = merchant.Id;
            transaction.Merchant = merchant;
        }

        // Apply tags
        if (!string.IsNullOrEmpty(rule.AssignTags))
        {
            var tagNames = rule.AssignTags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            foreach (var tagName in tagNames)
            {
                var tag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));

                if (tag == null)
                {
                    tag = new Tag { Name = tagName };
                    _context.Tags.Add(tag);
                    await _context.SaveChangesAsync();
                }

                // Check if transaction already has this tag
                var existingTransactionTag = await _context.TransactionTags
                    .FirstOrDefaultAsync(tt => tt.TransactionId == transaction.Id && tt.TagId == tag.Id);

                if (existingTransactionTag == null)
                {
                    _context.TransactionTags.Add(new TransactionTag
                    {
                        TransactionId = transaction.Id,
                        TagId = tag.Id
                    });
                }
            }
        }

        // Apply note
        if (!string.IsNullOrEmpty(rule.SetNote))
        {
            // Add note to ExtraInfo
            transaction.ExtraInfo = !string.IsNullOrEmpty(transaction.ExtraInfo) 
                ? $"{transaction.ExtraInfo}\nNote: {rule.SetNote}"
                : $"Note: {rule.SetNote}";
        }
    }
}