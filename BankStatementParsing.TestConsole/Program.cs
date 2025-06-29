using BankStatementParsing.Services;
using BankStatementParsing.Services.Parsers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

Console.WriteLine("=== MSKB PDF Parser Test ===");

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var parserLogger = loggerFactory.CreateLogger<MskbPdfParser>();
var serviceLogger = loggerFactory.CreateLogger<BankStatementParsingService>();

try
{
    // Create the parser
    var mskbParser = new MskbPdfParser(parserLogger);
    var parsers = new[] { mskbParser };
    var parsingService = new BankStatementParsingService(parsers, serviceLogger);

    // Path to the PDF file
    var pdfPath = @"..\AccountData\Account1\Inbox\4014293949_20250531_5_MSKB.pdf";
    
    Console.WriteLine($"Looking for PDF at: {Path.GetFullPath(pdfPath)}");
    
    if (!File.Exists(pdfPath))
    {
        Console.WriteLine($"Error: PDF file not found at {pdfPath}");
        Console.WriteLine("Current directory: " + Directory.GetCurrentDirectory());
        
        // Look for PDF files in the solution
        var solutionDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
        if (solutionDir != null)
        {
            var accountDataDir = Path.Combine(solutionDir, "AccountData");
            if (Directory.Exists(accountDataDir))
            {
                Console.WriteLine($"Searching in {accountDataDir}:");
                foreach (var file in Directory.GetFiles(accountDataDir, "*.pdf", SearchOption.AllDirectories))
                {
                    Console.WriteLine($"  Found PDF: {file}");
                    // Use the first PDF found
                    if (file.Contains("MSKB"))
                    {
                        pdfPath = file;
                        Console.WriteLine($"Using PDF: {pdfPath}");
                        break;
                    }
                }
            }
        }
        
        if (!File.Exists(pdfPath))
        {
            Console.WriteLine("No MSKB PDF file found. Please check the file location.");
            return;
        }
    }

    // Parse the PDF
    Console.WriteLine($"File size: {new FileInfo(pdfPath).Length} bytes");
    Console.WriteLine("Starting parsing...");

    using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
    var fileName = Path.GetFileName(pdfPath);
    
    var result = await parsingService.ParseStatementAsync(fileStream, fileName, "MSKB");

    // Display results
    Console.WriteLine("\n" + new string('=', 60));
    Console.WriteLine("PARSING RESULTS");
    Console.WriteLine(new string('=', 60));
    
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
            var desc = transaction.Description.Length > 40 
                ? transaction.Description.Substring(0, 37) + "..." 
                : transaction.Description;
                
            Console.WriteLine($"{transaction.Date:dd.MM.yyyy,-12} " +
                            $"{transaction.Type,-6} " +
                            $"{transaction.Amount,15:N2} " +
                            $"{desc,-40}");
        }

        if (result.Transactions.Count > 10)
        {
            Console.WriteLine($"... and {result.Transactions.Count - 10} more transactions");
        }
    }

    // Save results to JSON file for detailed inspection
    var jsonOptions = new JsonSerializerOptions 
    { 
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    var jsonResult = JsonSerializer.Serialize(result, jsonOptions);
    var jsonFileName = $"parsed_statement_{DateTime.Now:yyyyMMdd_HHmmss}.json";
    await File.WriteAllTextAsync(jsonFileName, jsonResult);
    
    Console.WriteLine($"\nDetailed results saved to: {jsonFileName}");
    Console.WriteLine("\n✅ Parsing completed successfully!");
    
    // Summary statistics
    var totalDebit = result.Transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
    var totalCredit = result.Transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
    
    Console.WriteLine($"\n📊 Transaction Summary:");
    Console.WriteLine($"   Total Debits:  {totalDebit:N2} {result.Currency}");
    Console.WriteLine($"   Total Credits: {totalCredit:N2} {result.Currency}");
    Console.WriteLine($"   Net Change:    {(totalCredit - totalDebit):N2} {result.Currency}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error occurred during parsing: {ex.Message}");
    Console.WriteLine($"\nStack trace:");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
