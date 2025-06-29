using BankStatementParsing.Core.Models;
using BankStatementParsing.Core.Entities;
using BankStatementParsing.Infrastructure;
using Microsoft.Extensions.Logging;

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

    public int ImportStatement(BankStatementData data)
    {
        // Try to find or create account
        var account = _db.Accounts.FirstOrDefault(a => a.AccountNumber == data.AccountNumber);
        if (account == null)
        {
            account = new Account
            {
                AccountNumber = data.AccountNumber,
                Name = data.AccountHolderName
            };
            _db.Accounts.Add(account);
            _db.SaveChanges();
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
            NumCredits = null
        };
        _db.Statements.Add(statement);
        _db.SaveChanges();

        // Import transactions
        int imported = 0;
        foreach (var t in data.Transactions)
        {
            bool exists = _db.Transactions.Any(x =>
                x.StatementId == statement.Id &&
                x.Date == t.Date &&
                x.Amount == (double)t.Amount &&
                x.Description == t.Description);
            if (exists)
                continue;

            var entity = new BankStatementParsing.Infrastructure.Transaction
            {
                StatementId = statement.Id,
                Date = t.Date,
                Description = t.Description,
                Amount = (double)t.Amount,
                Currency = data.Currency,
                Reference = t.Reference,
                MerchantId = null,
                Countervalue = null,
                OriginalCurrency = null,
                ExchangeRate = null,
                ExtraInfo = null
            };
            _db.Transactions.Add(entity);
            imported++;
        }
        _db.SaveChanges();
        _logger.LogInformation("Imported {Count} transactions for statement {StatementId}", imported, statement.Id);
        return imported;
    }
} 