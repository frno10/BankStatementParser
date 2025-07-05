using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace BankStatementParsing.CLI.Services;

public class ExtractionService : IExtractionService
{
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(ILogger<ExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractTextAsync(string pdfPath, string? outputDir = null, bool force = false, string? account = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExtractionResult
        {
            InputFile = pdfPath
        };

        try
        {
            if (!File.Exists(pdfPath))
            {
                result.Error = $"PDF file not found: {pdfPath}";
                return result;
            }

            var fileInfo = new FileInfo(pdfPath);
            result.FileSizeBytes = fileInfo.Length;

            // Determine output path
            var outputPath = outputDir != null 
                ? Path.Combine(outputDir, Path.ChangeExtension(Path.GetFileName(pdfPath), ".txt"))
                : Path.ChangeExtension(pdfPath, ".txt");

            // Check if output already exists and force is not set
            if (!force && File.Exists(outputPath))
            {
                result.OutputPath = outputPath;
                result.Success = true;
                result.Error = "File already exists (use --force to overwrite)";
                return result;
            }

            // Ensure output directory exists
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (outputDirectory != null && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Extract text from PDF
            using (var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
            {
                var extractedText = ExtractTextFromPdf(fileStream, out int pageCount);
                result.PageCount = pageCount;

                // Write extracted text to file
                await File.WriteAllTextAsync(outputPath, extractedText);
            }

            result.OutputPath = outputPath;
            result.Success = true;
            
            _logger.LogInformation("Successfully extracted text from {PdfPath} to {OutputPath} ({PageCount} pages, {SizeKB} KB)", 
                pdfPath, outputPath, result.PageCount, result.FileSizeBytes / 1024);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _logger.LogError(ex, "Failed to extract text from {PdfPath}", pdfPath);
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    private string ExtractTextFromPdf(Stream fileStream, out int pageCount)
    {
        // Use PdfPig to extract text from all pages with proper line preservation
        fileStream.Position = 0;
        using var document = PdfDocument.Open(fileStream);
        pageCount = document.NumberOfPages;
        
        var sb = new System.Text.StringBuilder();
        for (int pageNum = 1; pageNum <= document.NumberOfPages; pageNum++)
        {
            var page = document.GetPage(pageNum);
            // Group words by Y position (line) to preserve line structure
            var words = page.GetWords().ToList();
            var lines = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                .OrderByDescending(g => g.Key) // PDF Y=0 is bottom, so descending
                .ToList();
            
            foreach (var line in lines)
            {
                sb.AppendLine(string.Join(" ", line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            }
            sb.AppendLine(); // Extra newline between pages
        }
        
        fileStream.Position = 0;
        return sb.ToString();
    }
}