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

        public async Task RunAsync(bool force)
        {
            var pdfFiles = new List<string>();
            var solutionDir = Directory.GetCurrentDirectory();
            Console.WriteLine($"[DEBUG] Current Directory: {solutionDir}");
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
            int totalImported = 0;
            int totalProcessed = 0;
            foreach (var pdfPath in pdfFiles)
            {
                var txtPath = Path.ChangeExtension(pdfPath, ".txt");
                if (!force && File.Exists(txtPath))
                {
                    _logger.LogInformation("Skipping {File} (TXT exists)", Path.GetFileName(pdfPath));
                    continue;
                }
                _logger.LogInformation("Processing {File}...", Path.GetFileName(pdfPath));
                try
                {
                    using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
                    var fileName = Path.GetFileName(pdfPath);
                    var result = await _parsingService.ParseStatementAsync(fileStream, fileName, "Bank");
                    // Use a new scope for DbContext per import
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var importService = scope.ServiceProvider.GetRequiredService<BankStatementImportService>();
                        var importedCount = importService.ImportStatement(result);
                        totalImported += importedCount;
                        totalProcessed++;
                        _logger.LogInformation("Imported {Count} transactions from {File}.", importedCount, fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing {File}", pdfPath);
                }
            }
            _logger.LogInformation("Processed {Count} PDFs. Imported {Total} transactions in total.", totalProcessed, totalImported);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(cfg => cfg.AddConsole().SetMinimumLevel(LogLevel.Information));
                    var connectionString = "Data Source=../Database/bankstatements.db";
                    services.AddDbContext<BankStatementParsingContext>(options =>
                        options.UseSqlite(connectionString));
                    Console.WriteLine($"[DEBUG] Using SQLite connection string: {connectionString}");
                    if (connectionString.StartsWith("Data Source="))
                    {
                        var dbPath = connectionString.Substring("Data Source=".Length).Trim();
                        var absoluteDbPath = Path.GetFullPath(dbPath, AppContext.BaseDirectory);
                        Console.WriteLine($"[DEBUG] Absolute path to SQLite DB: {absoluteDbPath}");
                    }
                    services.AddTransient<PdfStatementParser>();
                    services.AddTransient<BankStatementParsingService>(sp =>
                        new BankStatementParsingService(new[] { sp.GetRequiredService<PdfStatementParser>() },
                            sp.GetRequiredService<ILogger<BankStatementParsingService>>()));
                    services.AddTransient<BankStatementImportService>();
                    services.AddTransient<BatchImportService>();
                })
                .Build();

            var force = args.Contains("--force");
            using var scope = host.Services.CreateScope();
            var batchService = scope.ServiceProvider.GetRequiredService<BatchImportService>();
            await batchService.RunAsync(force);
        }
    }
}
