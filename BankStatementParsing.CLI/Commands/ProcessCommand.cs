using BankStatementParsing.CLI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace BankStatementParsing.CLI.Commands;

public class ProcessCommand : BaseCommand
{
    public ProcessCommand(IServiceProvider serviceProvider) 
        : base("process", "Full pipeline: extract -> parse -> import PDF statements", serviceProvider)
    {
        var inputOption = new Option<string[]>("--input", "Input PDF file(s) or directory") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var bankOption = new Option<string>("--bank", "Bank type (CSOB, etc.)") { IsRequired = true };
        var accountOption = new Option<string?>("--account", "Account identifier");
        var workDirOption = new Option<string?>("--work-dir", "Working directory for intermediate files");
        var keepIntermediateOption = new Option<bool>("--keep-intermediate", "Keep intermediate files (txt, json)");
        var skipDuplicatesOption = new Option<bool>("--skip-duplicates", () => true, "Skip duplicate transactions");
        var dryRunOption = new Option<bool>("--dry-run", "Show what would be processed without importing to database");

        AddOption(inputOption);
        AddOption(bankOption);
        AddOption(accountOption);
        AddOption(workDirOption);
        AddOption(keepIntermediateOption);
        AddOption(skipDuplicatesOption);
        AddOption(dryRunOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var inputs = context.ParseResult.GetValueForOption(inputOption)!;
            var bank = context.ParseResult.GetValueForOption(bankOption)!;
            var account = context.ParseResult.GetValueForOption(accountOption);
            var workDir = context.ParseResult.GetValueForOption(workDirOption);
            var keepIntermediate = context.ParseResult.GetValueForOption(keepIntermediateOption);
            var skipDuplicates = context.ParseResult.GetValueForOption(skipDuplicatesOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);

            await ExecuteAsync(context, inputs, bank, account, workDir, keepIntermediate, skipDuplicates, dryRun);
        });
    }

    private async Task ExecuteAsync(InvocationContext context, string[] inputs, string bank, string? account, 
        string? workDir, bool keepIntermediate, bool skipDuplicates, bool dryRun)
    {
        try
        {
            var pipelineService = ServiceProvider.GetRequiredService<IPipelineService>();
            
            WriteVerbose($"Starting full pipeline for {inputs.Length} input(s)", context);
            
            if (dryRun)
            {
                WriteOutput("DRY RUN MODE - No data will be imported to database", context);
            }
            
            var files = await ResolveInputFiles(inputs, context);
            var results = new List<ProcessResult>();
            
            // Setup working directory
            var tempWorkDir = workDir ?? Path.Combine(Path.GetTempPath(), $"bankstmt_process_{DateTime.Now:yyyyMMdd_HHmmss}");
            if (!Directory.Exists(tempWorkDir))
            {
                Directory.CreateDirectory(tempWorkDir);
                WriteVerbose($"Created working directory: {tempWorkDir}", context);
            }
            
            try
            {
                foreach (var file in files)
                {
                    WriteOutput($"Processing: {Path.GetFileName(file)}", context);
                    
                    var result = await pipelineService.ProcessFileAsync(new ProcessRequest
                    {
                        InputFile = file,
                        Bank = bank,
                        Account = account,
                        WorkingDirectory = tempWorkDir,
                        SkipDuplicates = skipDuplicates,
                        DryRun = dryRun
                    });
                    
                    results.Add(result);
                    
                    if (result.Success)
                    {
                        var action = dryRun ? "Would import" : "Imported";
                        WriteOutput($"  ✓ {action} {result.TransactionCount} transactions", context);
                        
                        if (IsVerbose(context))
                        {
                            WriteVerbose($"    Extract: {result.ExtractStep?.Success} ({result.ExtractStep?.Duration.TotalSeconds:F1}s)", context);
                            WriteVerbose($"    Parse: {result.ParseStep?.Success} ({result.ParseStep?.Duration.TotalSeconds:F1}s)", context);
                            WriteVerbose($"    Import: {result.ImportStep?.Success} ({result.ImportStep?.Duration.TotalSeconds:F1}s)", context);
                        }
                    }
                    else
                    {
                        WriteError($"  ✗ Failed at {result.FailedStep}: {result.Error}");
                    }
                }
                
                // Output summary
                var successful = results.Count(r => r.Success);
                var failed = results.Count - successful;
                var totalTransactions = results.Where(r => r.Success).Sum(r => r.TransactionCount);
                var totalDuration = results.Sum(r => r.TotalDuration.TotalSeconds);
                
                var action = dryRun ? "would be imported" : "imported";
                WriteOutput($"Summary: {successful} successful, {failed} failed, {totalTransactions} transactions {action} in {totalDuration:F1}s", context);
                
                // Output results in specified format
                await OutputResults(results, context);
                
                context.ExitCode = failed > 0 ? 1 : 0;
            }
            finally
            {
                // Cleanup working directory if not keeping intermediate files
                if (!keepIntermediate && workDir == null && Directory.Exists(tempWorkDir))
                {
                    try
                    {
                        Directory.Delete(tempWorkDir, true);
                        WriteVerbose($"Cleaned up working directory: {tempWorkDir}", context);
                    }
                    catch (Exception ex)
                    {
                        WriteVerbose($"Failed to cleanup working directory: {ex.Message}", context);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            WriteError($"Processing failed: {ex.Message}");
            Logger.LogError(ex, "Process command failed");
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
                if (Path.GetExtension(input).ToLowerInvariant() == ".pdf")
                {
                    files.Add(Path.GetFullPath(input));
                }
                else
                {
                    WriteError($"File is not a PDF: {input}");
                }
            }
            else if (Directory.Exists(input))
            {
                var pdfFiles = Directory.GetFiles(input, "*.pdf", SearchOption.AllDirectories);
                files.AddRange(pdfFiles.Select(Path.GetFullPath));
                WriteVerbose($"Found {pdfFiles.Length} PDF files in directory: {input}", context);
            }
            else
            {
                WriteError($"Input not found: {input}");
            }
        }
        
        return files.Distinct().ToList();
    }

    private async Task OutputResults(List<ProcessResult> results, InvocationContext context)
    {
        var format = GetOutputFormat(context);
        
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                break;
                
            case "csv":
                Console.WriteLine("File,Success,TransactionCount,FailedStep,TotalDurationSeconds,Error");
                foreach (var result in results)
                {
                    Console.WriteLine($"\"{result.InputFile}\",{result.Success},{result.TransactionCount},\"{result.FailedStep}\",{result.TotalDuration.TotalSeconds:F1},\"{result.Error}\"");
                }
                break;
                
            default: // table format handled in ExecuteAsync
                break;
        }
    }
}