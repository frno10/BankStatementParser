using BankStatementParsing.CLI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace BankStatementParsing.CLI.Commands;

public class ParseCommand : BaseCommand
{
    public ParseCommand(IServiceProvider serviceProvider) 
        : base("parse", "Parse extracted text files into structured transaction data", serviceProvider)
    {
        var inputOption = new Option<string[]>("--input", "Input text file(s) or directory") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var bankOption = new Option<string>("--bank", "Bank type (CSOB, etc.)") { IsRequired = true };
        var outputOption = new Option<string?>("--output-dir", "Output directory for parsed data");
        var formatOption = new Option<string>("--format", () => "json", "Output format (json, csv)");
        var accountOption = new Option<string?>("--account", "Account identifier");

        AddOption(inputOption);
        AddOption(bankOption);
        AddOption(outputOption);
        AddOption(formatOption);
        AddOption(accountOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var inputs = context.ParseResult.GetValueForOption(inputOption)!;
            var bank = context.ParseResult.GetValueForOption(bankOption)!;
            var outputDir = context.ParseResult.GetValueForOption(outputOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var account = context.ParseResult.GetValueForOption(accountOption);

            await ExecuteAsync(context, inputs, bank, outputDir, format, account);
        });
    }

    private async Task ExecuteAsync(InvocationContext context, string[] inputs, string bank, string? outputDir, string format, string? account)
    {
        try
        {
            var parsingService = ServiceProvider.GetRequiredService<IParsingService>();
            
            WriteVerbose($"Starting parsing for {inputs.Length} input(s) with bank type: {bank}", context);
            
            var files = await ResolveInputFiles(inputs, context);
            var results = new List<ParseResult>();
            
            foreach (var file in files)
            {
                WriteVerbose($"Parsing file: {file}", context);
                
                var result = await parsingService.ParseFileAsync(file, bank, outputDir, format, account);
                results.Add(result);
                
                if (result.Success)
                {
                    WriteOutput($"✓ Parsed: {Path.GetFileName(file)} -> {result.TransactionCount} transactions", context);
                    if (!string.IsNullOrEmpty(result.OutputPath))
                    {
                        WriteVerbose($"  Output: {result.OutputPath}", context);
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
            var totalTransactions = results.Where(r => r.Success).Sum(r => r.TransactionCount);
            
            WriteOutput($"Summary: {successful} successful, {failed} failed, {totalTransactions} total transactions", context);
            
            // Output results in specified format
            await OutputResults(results, context);
            
            context.ExitCode = failed > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            WriteError($"Parsing failed: {ex.Message}");
            Logger.LogError(ex, "Parse command failed");
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
                if (Path.GetExtension(input).ToLowerInvariant() == ".txt")
                {
                    files.Add(Path.GetFullPath(input));
                }
                else
                {
                    WriteError($"File is not a text file: {input}");
                }
            }
            else if (Directory.Exists(input))
            {
                var txtFiles = Directory.GetFiles(input, "*.txt", SearchOption.AllDirectories);
                files.AddRange(txtFiles.Select(Path.GetFullPath));
                WriteVerbose($"Found {txtFiles.Length} text files in directory: {input}", context);
            }
            else
            {
                WriteError($"Input not found: {input}");
            }
        }
        
        return files.Distinct().ToList();
    }

    private async Task OutputResults(List<ParseResult> results, InvocationContext context)
    {
        var format = GetOutputFormat(context);
        
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                break;
                
            case "csv":
                Console.WriteLine("File,Success,TransactionCount,OutputPath,Error");
                foreach (var result in results)
                {
                    Console.WriteLine($"\"{result.InputFile}\",{result.Success},{result.TransactionCount},\"{result.OutputPath}\",\"{result.Error}\"");
                }
                break;
                
            default: // table format handled in ExecuteAsync
                break;
        }
    }
}