using BankStatementParsing.Services;
using BankStatementParsing.Services.Parsers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BankStatementParsing.Api;

public class TestPdfParser
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var parserLogger = loggerFactory.CreateLogger<PdfStatementParser>();
        var serviceLogger = loggerFactory.CreateLogger<BankStatementParsingService>();

        try
        {
            // Create the parser
            var pdfParser = new PdfStatementParser(parserLogger);
            var parsers = new[] { pdfParser };
            var parsingService = new BankStatementParsingService(parsers, serviceLogger);

            // Path to the PDF file
            var pdfPath = @"AccountData\Account1\Inbox\4014293949_20250531_5_MSKB.pdf";
            
            Console.WriteLine($"Attempting to parse PDF: {pdfPath}");
            
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"Error: PDF file not found at {pdfPath}");
                return 1;
            }

            // Parse the PDF
            using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
            var fileName = Path.GetFileName(pdfPath);
            
            Console.WriteLine($"File size: {new FileInfo(pdfPath).Length} bytes");
            Console.WriteLine("Starting parsing...");

            var result = await parsingService.ParseStatementAsync(fileStream, fileName, "MSKB");

            // Display results
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("PARSING RESULTS");
            Console.WriteLine(new string('=', 50));
            
            Console.WriteLine($"Bank Name: {result.BankName}");
            Console.WriteLine($"Account Number: {result.AccountNumber}");
            Console.WriteLine($"Account Holder: {result.AccountHolderName}");
            Console.WriteLine($"Currency: {result.Currency}");
            Console.WriteLine($"Statement Period: {result.StatementPeriodStart:dd.MM.yyyy} - {result.StatementPeriodEnd:dd.MM.yyyy}");
            Console.WriteLine($"Opening Balance: {result.OpeningBalance:N2} {result.Currency}");
            Console.WriteLine($"Closing Balance: {result.ClosingBalance:N2} {result.Currency}");
            Console.WriteLine($"Transactions Found: {result.Transactions.Count}");

            if (result.Transactions.Any())
            {
                Console.WriteLine("\nFIRST 10 TRANSACTIONS:");
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"{"Date",-12} {"Type",-6} {"Amount",-15} {"Description",-40}");
                Console.WriteLine(new string('-', 80));

                foreach (var transaction in result.Transactions.Take(10))
                {
                    Console.WriteLine($"{transaction.Date:dd.MM.yyyy,-12} " +
                                    $"{transaction.Type,-6} " +
                                    $"{transaction.Amount,15:N2} " +
                                    $"{transaction.Description,-40}");
                }

                if (result.Transactions.Count > 10)
                {
                    Console.WriteLine($"... and {result.Transactions.Count - 10} more transactions");
                }
            }

            // Save results to JSON file for inspection
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var jsonResult = JsonSerializer.Serialize(result, jsonOptions);
            var jsonFileName = $"parsed_statement_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await File.WriteAllTextAsync(jsonFileName, jsonResult);
            
            Console.WriteLine($"\nDetailed results saved to: {jsonFileName}");
            Console.WriteLine("\nParsing completed successfully!");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred during parsing: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
} 