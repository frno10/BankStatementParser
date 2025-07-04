using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BankStatementParsing.CLI.Services;

public class PipelineService : IPipelineService
{
    private readonly ILogger<PipelineService> _logger;
    private readonly IExtractionService _extractionService;
    private readonly IParsingService _parsingService;
    private readonly IImportService _importService;

    public PipelineService(
        ILogger<PipelineService> logger,
        IExtractionService extractionService,
        IParsingService parsingService,
        IImportService importService)
    {
        _logger = logger;
        _extractionService = extractionService;
        _parsingService = parsingService;
        _importService = importService;
    }

    public async Task<ProcessResult> ProcessFileAsync(ProcessRequest request)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var result = new ProcessResult
        {
            InputFile = request.InputFile
        };

        try
        {
            _logger.LogInformation("Starting pipeline processing for {InputFile}", request.InputFile);

            // Step 1: Extract text from PDF
            var extractResult = await _extractionService.ExtractTextAsync(
                request.InputFile, 
                request.WorkingDirectory, 
                force: true, 
                request.Account);

            result.ExtractStep = new StepResult
            {
                Success = extractResult.Success,
                Error = extractResult.Error,
                Duration = extractResult.Duration,
                OutputPath = extractResult.OutputPath
            };

            if (!extractResult.Success)
            {
                result.FailedStep = "Extract";
                result.Error = extractResult.Error;
                return result;
            }

            // Step 2: Parse extracted text
            var parseResult = await _parsingService.ParseFileAsync(
                extractResult.OutputPath!,
                request.Bank,
                request.WorkingDirectory,
                "json",
                request.Account);

            result.ParseStep = new StepResult
            {
                Success = parseResult.Success,
                Error = parseResult.Error,
                Duration = parseResult.Duration,
                OutputPath = parseResult.OutputPath
            };

            if (!parseResult.Success)
            {
                result.FailedStep = "Parse";
                result.Error = parseResult.Error;
                return result;
            }

            result.TransactionCount = parseResult.TransactionCount;

            // Step 3: Import to database (if not dry run)
            if (parseResult.OutputPath != null)
            {
                var importResult = await _importService.ImportFileAsync(
                    parseResult.OutputPath,
                    request.Account,
                    request.SkipDuplicates,
                    request.DryRun);

                result.ImportStep = new StepResult
                {
                    Success = importResult.Success,
                    Error = importResult.Error,
                    Duration = importResult.Duration
                };

                if (!importResult.Success)
                {
                    result.FailedStep = "Import";
                    result.Error = importResult.Error;
                    return result;
                }
            }

            result.Success = true;
            _logger.LogInformation("Successfully completed pipeline processing for {InputFile} ({TransactionCount} transactions)",
                request.InputFile, result.TransactionCount);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.FailedStep = "Pipeline";
            _logger.LogError(ex, "Pipeline processing failed for {InputFile}", request.InputFile);
        }
        finally
        {
            totalStopwatch.Stop();
            result.TotalDuration = totalStopwatch.Elapsed;
        }

        return result;
    }
}