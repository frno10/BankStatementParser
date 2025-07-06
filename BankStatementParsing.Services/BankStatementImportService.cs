using BankStatementParsing.Core.Models;
using BankStatementParsing.Core.Entities;
using BankStatementParsing.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace BankStatementParsing.Services;

public class BankStatementImportService
{
    private readonly BankStatementParsingContext _db;
    private readonly ILogger<BankStatementImportService> _logger;

    public BankStatementImportService(BankStatementParsingContext db, ILogger<BankStatementImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> ImportStatementAsync(BankStatementData data, string statementName = "")
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Try to find or create account
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == data.AccountNumber);
            if (account == null)
            {
                account = new Account
                {
                    AccountNumber = data.AccountNumber,
                    Name = data.AccountHolderName
                };
                _db.Accounts.Add(account);
                await _db.SaveChangesAsync();
            }

            // Create statement
            var statement = new Statement
            {
                AccountId = account.Id,
                PeriodStart = data.StatementPeriodStart,
                PeriodEnd = data.StatementPeriodEnd,
                StatementNumber = null,
                OpeningBalance = (double?)data.OpeningBalance,
                ClosingBalance = (double?)data.ClosingBalance,
                TotalDebits = null,
                TotalCredits = null,
                NumDebits = null,
                NumCredits = null,
                StatementName = statementName ?? string.Empty
            };
            _db.Statements.Add(statement);
            await _db.SaveChangesAsync();

            // Pre-load existing transactions for this statement to avoid N+1 queries
            var existingTransactions = (await _db.Transactions
                .Where(x => x.StatementId == statement.Id)
                .Select(x => new { x.Date, x.Amount, x.Description })
                .ToListAsync()).ToHashSet();

            // Import transactions in batch
            int imported = 0;
            var transactionsToAdd = new List<BankStatementParsing.Infrastructure.Transaction>();
            var merchantCache = new Dictionary<string, int>();

            foreach (var t in data.Transactions)
            {
                // Check for duplicates using the pre-loaded set
                bool exists = existingTransactions.Any(x =>
                    x.Date == t.Date &&
                    x.Amount == (double)t.Amount &&
                    x.Description == t.Description);
                
                if (exists)
                {
                    _logger.LogDebug("Skipping duplicate transaction: {Date} {Amount} {Description}", 
                        t.Date, t.Amount, t.Description);
                    continue;
                }

                int? merchantId = null;
                if (!string.IsNullOrWhiteSpace(t.MerchantName))
                {
                    // Use cache to avoid repeated database lookups
                    if (!merchantCache.TryGetValue(t.MerchantName.ToLower(), out int cachedMerchantId))
                    {
                        var merchant = await _db.Merchants.FirstOrDefaultAsync(m => m.Name.ToLower() == t.MerchantName.ToLower());
                        if (merchant == null)
                        {
                            merchant = new Merchant { Name = t.MerchantName };
                            _db.Merchants.Add(merchant);
                            await _db.SaveChangesAsync();
                        }
                        merchantId = merchant.Id;
                        merchantCache[t.MerchantName.ToLower()] = merchantId.Value;
                    }
                    else
                    {
                        merchantId = cachedMerchantId;
                    }
                }

                var entity = new BankStatementParsing.Infrastructure.Transaction
                {
                    StatementId = statement.Id,
                    Date = t.Date,
                    Description = t.Description,
                    Amount = (double)t.Amount,
                    Currency = data.Currency,
                    Reference = t.Reference,
                    MerchantId = merchantId,
                    Countervalue = t.Countervalue.HasValue ? (double?)t.Countervalue.Value : null,
                    OriginalCurrency = null,
                    ExchangeRate = t.ExchangeRate.HasValue ? (double?)t.ExchangeRate.Value : null,
                    ExtraInfo = null
                };
                
                transactionsToAdd.Add(entity);
                imported++;
            }

            // Add all transactions in a batch
            if (transactionsToAdd.Count > 0)
            {
                _db.Transactions.AddRange(transactionsToAdd);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Successfully imported {Count} transactions for statement {StatementId}", imported, statement.Id);
            return imported;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to import statement: {Error}", ex.Message);
            throw;
        }
    }

    // Keep synchronous version for backward compatibility
    public int ImportStatement(BankStatementData data, string statementName = "")
    {
        return ImportStatementAsync(data, statementName).GetAwaiter().GetResult();
    }
} 