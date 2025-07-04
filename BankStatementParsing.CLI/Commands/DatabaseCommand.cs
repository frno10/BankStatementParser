using BankStatementParsing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace BankStatementParsing.CLI.Commands;

public class DatabaseCommand : BaseCommand
{
    public DatabaseCommand(IServiceProvider serviceProvider) 
        : base("database", "Database management operations", serviceProvider)
    {
        var clearCommand = new Command("clear", "Clear database data");
        var allOption = new Option<bool>("--all", "Clear all data including merchants and tags");
        var confirmedOption = new Option<bool>("--confirmed", "Confirm the operation without prompting");

        clearCommand.AddOption(allOption);
        clearCommand.AddOption(confirmedOption);

        clearCommand.SetHandler(async (InvocationContext context) =>
        {
            var clearAll = context.ParseResult.GetValueForOption(allOption);
            var confirmed = context.ParseResult.GetValueForOption(confirmedOption);

            await ClearDatabaseAsync(context, clearAll, confirmed);
        });

        var statusCommand = new Command("status", "Show database status");
        statusCommand.SetHandler(async (InvocationContext context) =>
        {
            await ShowDatabaseStatusAsync(context);
        });

        AddCommand(clearCommand);
        AddCommand(statusCommand);
    }

    private async Task ClearDatabaseAsync(InvocationContext context, bool clearAll, bool confirmed)
    {
        try
        {
            if (!confirmed)
            {
                var warningMessage = clearAll 
                    ? "This will delete ALL data from the database (transactions, statements, accounts, merchants, tags). This action is irreversible."
                    : "This will delete transaction data and statements but keep merchants and tags. This action is irreversible.";
                
                WriteOutput($"WARNING: {warningMessage}", context);
                WriteOutput("Use --confirmed to proceed without this prompt.", context);
                context.ExitCode = 1;
                return;
            }

            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();

            WriteVerbose("Starting database clear operation", context);

            if (clearAll)
            {
                await ClearAllDataAsync(dbContext);
                WriteOutput("✓ All database data has been cleared", context);
            }
            else
            {
                await ClearAllExceptMerchantsAndTagsAsync(dbContext);
                WriteOutput("✓ Transaction data cleared (merchants and tags preserved)", context);
            }
        }
        catch (Exception ex)
        {
            WriteError($"Database clear failed: {ex.Message}");
            Logger.LogError(ex, "Database clear operation failed");
            context.ExitCode = 1;
        }
    }

    private async Task ShowDatabaseStatusAsync(InvocationContext context)
    {
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();

            var accountCount = await dbContext.Accounts.CountAsync();
            var statementCount = await dbContext.Statements.CountAsync();
            var transactionCount = await dbContext.Transactions.CountAsync();
            var merchantCount = await dbContext.Merchants.CountAsync();
            var tagCount = await dbContext.Tags.CountAsync();

            WriteOutput("Database Status:", context);
            WriteOutput($"  Accounts: {accountCount}", context);
            WriteOutput($"  Statements: {statementCount}", context);
            WriteOutput($"  Transactions: {transactionCount}", context);
            WriteOutput($"  Merchants: {merchantCount}", context);
            WriteOutput($"  Tags: {tagCount}", context);

            // Database file info
            var connectionString = dbContext.Database.GetConnectionString();
            if (connectionString?.Contains("Data Source=") == true)
            {
                var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0];
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    WriteOutput($"  Database file: {dbPath}", context);
                    WriteOutput($"  File size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB", context);
                    WriteOutput($"  Last modified: {fileInfo.LastWriteTime}", context);
                }
            }
        }
        catch (Exception ex)
        {
            WriteError($"Failed to get database status: {ex.Message}");
            Logger.LogError(ex, "Database status check failed");
            context.ExitCode = 1;
        }
    }

    private async Task ClearAllDataAsync(BankStatementParsingContext db)
    {
        // Remove join tables first due to FK constraints
        db.TransactionTags.RemoveRange(db.TransactionTags);
        db.MerchantTags.RemoveRange(db.MerchantTags);
        db.Transactions.RemoveRange(db.Transactions);
        db.Statements.RemoveRange(db.Statements);
        db.Accounts.RemoveRange(db.Accounts);
        db.Merchants.RemoveRange(db.Merchants);
        db.Tags.RemoveRange(db.Tags);
        await db.SaveChangesAsync();
    }

    private async Task ClearAllExceptMerchantsAndTagsAsync(BankStatementParsingContext db)
    {
        // Remove join tables and all except Merchants, Tags, and their join table
        db.TransactionTags.RemoveRange(db.TransactionTags);
        db.Transactions.RemoveRange(db.Transactions);
        db.Statements.RemoveRange(db.Statements);
        db.Accounts.RemoveRange(db.Accounts);
        await db.SaveChangesAsync();
    }
}