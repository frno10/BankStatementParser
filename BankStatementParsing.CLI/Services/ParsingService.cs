using BankStatementParsing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BankStatementParsing.CLI.Services;

public class ParsingService : IParsingService
{
    private readonly ILogger<ParsingService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ParsingService(ILogger<ParsingService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<ParseResult> ParseFileAsync(string textPath, string bankType, string? outputDir = null, string format = "json", string? account = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ParseResult
        {
            InputFile = textPath,
            BankType = bankType
        };

        try
        {
            if (!File.Exists(textPath))
            {
                result.Error = $"Text file not found: {textPath}";
                return result;
            }

            // Read the text content
            var statementText = await File.ReadAllTextAsync(textPath);
            var fileName = Path.GetFileName(textPath);

            using var scope = _serviceProvider.CreateScope();
            var parsingService = scope.ServiceProvider.GetRequiredService<BankStatementParsingService>();

            // Parse the statement
            using var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(statementText));
            var statement = await parsingService.ParseStatementAsync(textStream, fileName, bankType);

            if (statement == null || statement.Transactions.Count == 0)
            {
                result.Error = $"Failed to parse statement or no transactions found";
                return result;
            }

            result.TransactionCount = statement.Transactions.Count;
            result.StatementStartDate = statement.StatementPeriodStart;
            result.StatementEndDate = statement.StatementPeriodEnd;
            result.OpeningBalance = statement.OpeningBalance;
            result.ClosingBalance = statement.ClosingBalance;

            // Generate output if requested
            if (outputDir != null)
            {
                var outputPath = Path.Combine(outputDir, 
                    Path.ChangeExtension(Path.GetFileName(textPath), $".{format}"));

                // Ensure output directory exists
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Write output in requested format
                switch (format.ToLowerInvariant())
                {
                    case "json":
                        var json = JsonSerializer.Serialize(statement, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(outputPath, json);
                        break;
                        
                    case "csv":
                        await WriteCsvAsync(outputPath, statement);
                        break;
                        
                    default:
                        result.Error = $"Unsupported output format: {format}";
                        return result;
                }

                result.OutputPath = outputPath;
            }

            result.Success = true;
            
            _logger.LogInformation("Successfully parsed {TextPath} ({TransactionCount} transactions, {BankType})", 
                textPath, result.TransactionCount, bankType);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _logger.LogError(ex, "Failed to parse {TextPath} with bank type {BankType}", textPath, bankType);
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task WriteCsvAsync(string outputPath, object statement)
    {
        // This is a simplified CSV output - you may want to customize this based on your transaction model
        var lines = new List<string>
        {
            "Date,Description,Amount,Balance,Type"
        };

        // Use reflection to get transactions - adapt this to your actual model
        var transactionsProperty = statement.GetType().GetProperty("Transactions");
        if (transactionsProperty?.GetValue(statement) is IEnumerable<object> transactions)
        {
            foreach (var transaction in transactions)
            {
                var type = transaction.GetType();
                var date = type.GetProperty("Date")?.GetValue(transaction)?.ToString() ?? "";
                var description = type.GetProperty("Description")?.GetValue(transaction)?.ToString() ?? "";
                var amount = type.GetProperty("Amount")?.GetValue(transaction)?.ToString() ?? "";
                var balance = type.GetProperty("Balance")?.GetValue(transaction)?.ToString() ?? "";
                var transactionType = type.GetProperty("Type")?.GetValue(transaction)?.ToString() ?? "";

                lines.Add($"\"{date}\",\"{description}\",\"{amount}\",\"{balance}\",\"{transactionType}\"");
            }
        }

        await File.WriteAllLinesAsync(outputPath, lines);
    }
}