using BankStatementParsing.Services;
using BankStatementParsing.Services.Parsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BankStatementParsing.Infrastructure;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace BankStatementParsing.TestConsole
{
    public class BatchImportService
    {
        private readonly ILogger<BatchImportService> _logger;
        private readonly PdfStatementParser _pdfParser;
        private readonly BankStatementParsingService _parsingService;
        private readonly BankStatementImportService _importService;
        private readonly IServiceProvider _serviceProvider;

        public BatchImportService(
            ILogger<BatchImportService> logger,
            PdfStatementParser pdfParser,
            BankStatementParsingService parsingService,
            BankStatementImportService importService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _pdfParser = pdfParser;
            _parsingService = parsingService;
            _importService = importService;
            _serviceProvider = serviceProvider;
        }

        public async Task ExtractTextFromPdfsAsync(bool force)
        {
            var pdfFiles = new List<string>();
            
            // Find solution root by looking for .sln file
            var currentDir = Directory.GetCurrentDirectory();
            var solutionDir = currentDir;
            
            // Navigate up from bin/Debug/net9.0 to find the solution root
            for (int i = 0; i < 5 && solutionDir != null; i++)
            {
                if (File.Exists(Path.Combine(solutionDir, "BankStatementParsing.sln")))
                    break;
                solutionDir = Path.GetDirectoryName(solutionDir);
            }
            
            Console.WriteLine($"[DEBUG] Current Directory: {currentDir}");
            Console.WriteLine($"[DEBUG] Solution Directory: {solutionDir}");
            
            if (solutionDir != null)
            {
                var accountDataDir = Path.Combine(solutionDir, "AccountData");
                Console.WriteLine($"[DEBUG] Resolved AccountData Dir: {accountDataDir}");
                if (Directory.Exists(accountDataDir))
                {
                    var found = Directory.GetFiles(accountDataDir, "*.pdf", SearchOption.AllDirectories);
                    Console.WriteLine($"[DEBUG] Found {found.Length} PDF(s):");
                    foreach (var f in found) Console.WriteLine($"  [DEBUG] {f}");
                    pdfFiles.AddRange(found);
                }
                else
                {
                    Console.WriteLine($"[DEBUG] AccountData directory does not exist at: {accountDataDir}");
                }
            }
            if (pdfFiles.Count == 0)
            {
                _logger.LogWarning("No PDF files found in AccountData.");
                return;
            }
            _logger.LogInformation("Found {Count} PDF files.", pdfFiles.Count);
            int totalProcessed = 0;
            foreach (var pdfPath in pdfFiles)
            {
                var txtPath = Path.ChangeExtension(pdfPath, ".txt");
                if (!force && File.Exists(txtPath))
                {
                    _logger.LogInformation("Skipping {File} (TXT exists)", Path.GetFileName(pdfPath));
                    continue;
                }
                _logger.LogInformation("Extracting text from {File}...", Path.GetFileName(pdfPath));
                try
                {
                    // Extract text from PDF and write to .txt file
                    using (var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read))
                    {
                        var text = ExtractTextFromPdf(fileStream);
                        File.WriteAllText(txtPath, text);
                    }
                    totalProcessed++;
                    _logger.LogInformation("Extracted text to {File}", Path.GetFileName(txtPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting text from {File}", pdfPath);
                }
            }
            _logger.LogInformation("Processed {Count} PDFs. Extracted text to TXT files.", totalProcessed);
        }

        public async Task ParseTxtFilesAsync(bool force)
        {
            var txtFiles = new List<string>();
            
            // Find solution root by looking for .sln file
            var currentDir = Directory.GetCurrentDirectory();
            var solutionDir = currentDir;
            
            // Navigate up from bin/Debug/net9.0 to find the solution root
            for (int i = 0; i < 5 && solutionDir != null; i++)
            {
                if (File.Exists(Path.Combine(solutionDir, "BankStatementParsing.sln")))
                    break;
                solutionDir = Path.GetDirectoryName(solutionDir);
            }
            
            if (solutionDir != null)
            {
                var accountDataDir = Path.Combine(solutionDir, "AccountData");
                if (Directory.Exists(accountDataDir))
                {
                    var found = Directory.GetFiles(accountDataDir, "*.txt", SearchOption.AllDirectories);
                    txtFiles.AddRange(found);
                }
            }
            
            if (txtFiles.Count == 0)
            {
                _logger.LogWarning("No TXT files found in AccountData.");
                return;
            }
            
            _logger.LogInformation("Found {Count} TXT files.", txtFiles.Count);
            int totalImported = 0;
            int totalProcessed = 0;
            
            foreach (var txtPath in txtFiles)
            {
                _logger.LogInformation("Parsing {File}...", Path.GetFileName(txtPath));
                try
                {
                    // Use the .txt file for parsing
                    string statementText = File.ReadAllText(txtPath);
                    var fileName = Path.GetFileName(txtPath);
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var importService = scope.ServiceProvider.GetRequiredService<BankStatementImportService>();
                        var parsingService = scope.ServiceProvider.GetRequiredService<BankStatementParsingService>();
                        
                        // Use the parsing service with stream
                        using var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(statementText));
                        var result = await parsingService.ParseStatementAsync(textStream, fileName, "CSOB");
                        
                        if (result == null || result.Transactions.Count == 0)
                        {
                            _logger.LogError("Failed to parse statement: {File}", fileName);
                            continue;
                        }
                        
                        var importedCount = importService.ImportStatement(result, fileName);
                        totalImported += importedCount;
                        totalProcessed++;
                        _logger.LogInformation("Imported {Count} transactions from {File}.", importedCount, fileName);

                        // Move TXT and associated PDF to Processed folder if import was successful
                        if (importedCount > 0)
                        {
                            var processedDir = Path.Combine(Path.GetDirectoryName(txtPath)!, "Processed");
                            if (!Directory.Exists(processedDir))
                                Directory.CreateDirectory(processedDir);
                            var destTxt = Path.Combine(processedDir, Path.GetFileName(txtPath));
                            File.Move(txtPath, destTxt, overwrite: true);
                            var pdfPath = Path.ChangeExtension(txtPath, ".pdf");
                            if (File.Exists(pdfPath))
                            {
                                var destPdf = Path.Combine(processedDir, Path.GetFileName(pdfPath));
                                File.Move(pdfPath, destPdf, overwrite: true);
                            }
                            _logger.LogInformation("Moved {0} and associated PDF to {1}", Path.GetFileName(txtPath), processedDir);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing {File}", txtPath);
                }
            }
            _logger.LogInformation("Processed {Count} TXT files. Imported {Total} transactions in total.", totalProcessed, totalImported);
        }

        private string ExtractTextFromPdf(Stream fileStream)
        {
            // Use PdfPig to extract text from all pages with proper line preservation
            fileStream.Position = 0;
            using var document = PdfDocument.Open(fileStream);
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

    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(cfg => {
                        cfg.AddConsole();
                        cfg.SetMinimumLevel(LogLevel.Warning);
                        cfg.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                    });
                    // Compute absolute path to the shared database
                    var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
                    var absoluteDbPath = Path.Combine(solutionRoot, "Database", "bankstatements.db");
                    var connectionString = $"Data Source={absoluteDbPath}";
                    services.AddDbContext<BankStatementParsingContext>(options =>
                        options.UseSqlite(connectionString));
                    Console.WriteLine($"[DEBUG] Using SQLite connection string: {connectionString}");
                    Console.WriteLine($"[DEBUG] Absolute path to SQLite DB: {absoluteDbPath}");
                    services.AddTransient<PdfStatementParser>();
                    services.AddTransient<BankStatementParsingService>(sp =>
                        new BankStatementParsingService(new[] { sp.GetRequiredService<PdfStatementParser>() },
                            sp.GetRequiredService<ILogger<BankStatementParsingService>>()));
                    services.AddTransient<BankStatementImportService>();
                    services.AddTransient<BatchImportService>();
                })
                .Build();

            while (true)
            {
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. Exit");
                Console.WriteLine("2. Delete all EXCEPT Merchants (Places), Tags, and their join table");
                Console.WriteLine("3. Delete ALL data (irreversible)");
                Console.WriteLine("4. Extract text from PDFs to TXT files");
                Console.WriteLine("5. Parse TXT files and import to database");
                Console.Write("Enter your choice (1/2/3/4/5): ");
                var choice = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(choice)) choice = "1";

                Console.WriteLine(); // Add a newline after selecting an option

                using var scope = host.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();

                if (choice == "1")
                {
                    Console.WriteLine("[INFO] Exiting.");
                    break;
                }
                else if (choice == "2")
                {
                    await ClearAllExceptMerchantsAndTagsAsync(db);
                    Console.WriteLine("[INFO] All data except Merchants, Tags, and their join table deleted.");
                }
                else if (choice == "3")
                {
                    await ClearAllDataAsync(db);
                    Console.WriteLine("[INFO] All data deleted.");
                }
                else if (choice == "4")
                {
                    var force = args.Contains("--force");
                    var batchService = scope.ServiceProvider.GetRequiredService<BatchImportService>();
                    await batchService.ExtractTextFromPdfsAsync(force);
                }
                else if (choice == "5")
                {
                    var force = args.Contains("--force");
                    var batchService = scope.ServiceProvider.GetRequiredService<BatchImportService>();
                    await batchService.ParseTxtFilesAsync(force);
                }
                else
                {
                    Console.WriteLine("[INFO] Invalid option. Please try again.");
                }
                Console.WriteLine();
            }
        }

        private static async Task ClearAllDataAsync(BankStatementParsingContext db)
        {
            // Remove join tables first due to FK constraints
            db.TransactionTags.RemoveRange(db.TransactionTags);
            db.MerchantTags.RemoveRange(db.MerchantTags);
            db.Transactions.RemoveRange(db.Transactions);
            db.Statements.RemoveRange(db.Statements);
            db.Accounts.RemoveRange(db.Accounts);
            db.Merchants.RemoveRange(db.Merchants);
            db.Tags.RemoveRange(db.Tags);
            await db.SaveChangesAsync();
        }

        private static async Task ClearAllExceptMerchantsAndTagsAsync(BankStatementParsingContext db)
        {
            // Remove join tables and all except Merchants, Tags, and their join table
            db.TransactionTags.RemoveRange(db.TransactionTags);
            db.Transactions.RemoveRange(db.Transactions);
            db.Statements.RemoveRange(db.Statements);
            db.Accounts.RemoveRange(db.Accounts);
            await db.SaveChangesAsync();
        }
    }
}
