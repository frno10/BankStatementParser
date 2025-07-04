using BankStatementParsing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace BankStatementParsing.CLI.Services;

public class ImportService : IImportService
{
    private readonly ILogger<ImportService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ImportService(ILogger<ImportService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<ImportResult> ImportFileAsync(string dataPath, string? account = null, bool skipDuplicates = true, bool dryRun = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult
        {
            InputFile = dataPath,
            Account = account
        };

        try
        {
            if (!File.Exists(dataPath))
            {
                result.Error = $"Data file not found: {dataPath}";
                return result;
            }

            var extension = Path.GetExtension(dataPath).ToLowerInvariant();
            object? statementData = null;

            // Parse the input file based on its format
            switch (extension)
            {
                case ".json":
                    var jsonContent = await File.ReadAllTextAsync(dataPath);
                    statementData = JsonSerializer.Deserialize<object>(jsonContent);
                    break;
                    
                case ".csv":
                    // For CSV, you'd need to implement CSV parsing logic
                    result.Error = "CSV import not yet implemented";
                    return result;
                    
                default:
                    result.Error = $"Unsupported file format: {extension}";
                    return result;
            }

            if (statementData == null)
            {
                result.Error = "Failed to parse input data";
                return result;
            }

            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<BankStatementImportService>();

            if (dryRun)
            {
                // For dry run, we need to count what would be imported without actually importing
                // This is a simplified version - you may need to adapt based on your import service
                result.TotalCount = CountTransactions(statementData);
                result.ImportedCount = result.TotalCount; // Assume all would be imported in dry run
                result.SkippedCount = 0;
                result.Success = true;
                
                _logger.LogInformation("DRY RUN: Would import {TotalCount} transactions from {DataPath}", 
                    result.TotalCount, dataPath);
            }
            else
            {
                // Perform actual import
                var fileName = Path.GetFileName(dataPath);
                var importedCount = importService.ImportStatement(statementData, fileName);
                
                result.ImportedCount = importedCount;
                result.TotalCount = CountTransactions(statementData);
                result.SkippedCount = result.TotalCount - result.ImportedCount;
                result.Success = true;
                
                _logger.LogInformation("Successfully imported {ImportedCount}/{TotalCount} transactions from {DataPath}", 
                    result.ImportedCount, result.TotalCount, dataPath);
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _logger.LogError(ex, "Failed to import {DataPath}", dataPath);
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private int CountTransactions(object statementData)
    {
        // Use reflection to count transactions - adapt this to your actual model
        var transactionsProperty = statementData.GetType().GetProperty("Transactions");
        if (transactionsProperty?.GetValue(statementData) is IEnumerable<object> transactions)
        {
            return transactions.Count();
        }
        
        return 0;
    }
}