using BankStatementParsing.CLI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BankStatementParsing.CLI.Commands;

public class ImportCommand : BaseCommand
{
    public ImportCommand(IServiceProvider serviceProvider) 
        : base("import", "Import parsed transaction data into the database", serviceProvider)
    {
        var inputOption = new Option<string[]>("--input", "Input file(s) or directory containing parsed data") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var accountOption = new Option<string?>("--account", "Account identifier");
        var skipDuplicatesOption = new Option<bool>("--skip-duplicates", () => true, "Skip duplicate transactions");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be imported without actually importing");

        AddOption(inputOption);
        AddOption(accountOption);
        AddOption(skipDuplicatesOption);
        AddOption(dryRunOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var inputs = context.ParseResult.GetValueForOption(inputOption)!;
            var account = context.ParseResult.GetValueForOption(accountOption);
            var skipDuplicates = context.ParseResult.GetValueForOption(skipDuplicatesOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

            await ExecuteAsync(context, inputs, account, skipDuplicates, dryRun);
        });
    }

    private async Task ExecuteAsync(InvocationContext context, string[] inputs, string? account, bool skipDuplicates, bool dryRun)
    {
        try
        {
            var importService = ServiceProvider.GetRequiredService<IImportService>();
            
            WriteVerbose($"Starting import for {inputs.Length} input(s)", context);
            if (dryRun)
            {
                WriteOutput("DRY RUN MODE - No data will be imported", context);
            }
            
            var files = await ResolveInputFiles(inputs, context);
            var results = new List<ImportResult>();
            
            foreach (var file in files)
            {
                WriteVerbose($"Importing file: {file}", context);
                
                var result = await importService.ImportFileAsync(file, account, skipDuplicates, dryRun);
                results.Add(result);
                
                if (result.Success)
                {
                    var action = dryRun ? "Would import" : "Imported";
                    WriteOutput($"✓ {action}: {Path.GetFileName(file)} -> {result.ImportedCount} transactions", context);
                    if (result.SkippedCount > 0)
                    {
                        WriteVerbose($"  Skipped {result.SkippedCount} duplicates", context);
                    }
                }
                else
                {
                    WriteError($"✗ Failed: {Path.GetFileName(file)} - {result.Error}");
                }
            }
            
            // Output summary
            var successful = results.Count(r => r.Success);
            var failed = results.Count - successful;
            var totalImported = results.Where(r => r.Success).Sum(r => r.ImportedCount);
            var totalSkipped = results.Where(r => r.Success).Sum(r => r.SkippedCount);
            
            var summaryAction = dryRun ? "would be imported" : "imported";
            WriteOutput($"Summary: {successful} successful, {failed} failed, {totalImported} transactions {summaryAction}, {totalSkipped} skipped", context);
            
            // Output results in specified format
            await OutputResults(results, context);
            
            context.ExitCode = failed > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            WriteError($"Import failed: {ex.Message}");
            Logger.LogError(ex, "Import command failed");
            context.ExitCode = 1;
        }
    }

    private async Task<List<string>> ResolveInputFiles(string[] inputs, InvocationContext context)
    {
        var files = new List<string>();
        
        foreach (var input in inputs)
        {
            if (File.Exists(input))
            {
                var ext = Path.GetExtension(input).ToLowerInvariant();
                if (ext == ".json" || ext == ".csv")
                {
                    files.Add(Path.GetFullPath(input));
                }
                else
                {
                    WriteError($"File format not supported: {input} (expected .json or .csv)");
                }
            }
            else if (Directory.Exists(input))
            {
                var dataFiles = Directory.GetFiles(input, "*.json", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(input, "*.csv", SearchOption.AllDirectories))
                    .ToArray();
                files.AddRange(dataFiles.Select(Path.GetFullPath));
                WriteVerbose($"Found {dataFiles.Length} data files in directory: {input}", context);
            }
            else
            {
                WriteError($"Input not found: {input}");
            }
        }
        
        return files.Distinct().ToList();
    }

    private async Task OutputResults(List<ImportResult> results, InvocationContext context)
    {
        var format = GetOutputFormat(context);
        
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                break;
                
            case "csv":
                Console.WriteLine("File,Success,ImportedCount,SkippedCount,Error");
                foreach (var result in results)
                {
                    Console.WriteLine($"\"{result.InputFile}\",{result.Success},{result.ImportedCount},{result.SkippedCount},\"{result.Error}\"");
                }
                break;
                
            default: // table format handled in ExecuteAsync
                break;
        }
    }
}