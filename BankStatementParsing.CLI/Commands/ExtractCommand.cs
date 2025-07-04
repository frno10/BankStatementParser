using BankStatementParsing.CLI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace BankStatementParsing.CLI.Commands;

public class ExtractCommand : BaseCommand
{
    public ExtractCommand(IServiceProvider serviceProvider) 
        : base("extract", "Extract text from PDF bank statements", serviceProvider)
    {
        var inputOption = new Option<string[]>("--input", "Input PDF file(s) or directory") { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var outputOption = new Option<string?>("--output-dir", "Output directory for extracted text files");
        var forceOption = new Option<bool>("--force", "Overwrite existing text files");
        var accountOption = new Option<string?>("--account", "Account identifier for processing");

        AddOption(inputOption);
        AddOption(outputOption);
        AddOption(forceOption);
        AddOption(accountOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var inputs = context.ParseResult.GetValueForOption(inputOption)!;
            var outputDir = context.ParseResult.GetValueForOption(outputOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            var account = context.ParseResult.GetValueForOption(accountOption);

            await ExecuteAsync(context, inputs, outputDir, force, account);
        });
    }

    private async Task ExecuteAsync(InvocationContext context, string[] inputs, string? outputDir, bool force, string? account)
    {
        try
        {
            var extractionService = ServiceProvider.GetRequiredService<IExtractionService>();
            
            WriteVerbose($"Starting extraction for {inputs.Length} input(s)", context);
            
            var files = await ResolveInputFiles(inputs, context);
            var results = new List<ExtractionResult>();
            
            foreach (var file in files)
            {
                WriteVerbose($"Processing file: {file}", context);
                
                var result = await extractionService.ExtractTextAsync(file, outputDir, force, account);
                results.Add(result);
                
                if (result.Success)
                {
                    WriteOutput($"✓ Extracted: {Path.GetFileName(file)} -> {result.OutputPath}", context);
                }
                else
                {
                    WriteError($"✗ Failed: {Path.GetFileName(file)} - {result.Error}");
                }
            }
            
            // Output summary
            var successful = results.Count(r => r.Success);
            var failed = results.Count - successful;
            
            WriteOutput($"Summary: {successful} successful, {failed} failed", context);
            
            // Output results in specified format
            await OutputResults(results, context);
            
            context.ExitCode = failed > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            WriteError($"Extraction failed: {ex.Message}");
            Logger.LogError(ex, "Extraction command failed");
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

    private async Task OutputResults(List<ExtractionResult> results, InvocationContext context)
    {
        var format = GetOutputFormat(context);
        
        switch (format.ToLowerInvariant())
        {
            case "json":
                var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                break;
                
            case "csv":
                Console.WriteLine("File,Success,OutputPath,Error");
                foreach (var result in results)
                {
                    Console.WriteLine($"\"{result.InputFile}\",{result.Success},\"{result.OutputPath}\",\"{result.Error}\"");
                }
                break;
                
            default: // table format handled in ExecuteAsync
                break;
        }
    }
}