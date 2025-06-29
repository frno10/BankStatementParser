using BankStatementParsing.Services;
using BankStatementParsing.Services.Parsers;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BankStatementParsing.Infrastructure;
using System.Collections.Generic;

namespace BankStatementParsing.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            bool force = args.Contains("--force");
            var pdfFiles = new List<string>();
            var solutionDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
            if (solutionDir != null)
            {
                var accountDataDir = Path.Combine(solutionDir, "AccountData");
                if (Directory.Exists(accountDataDir))
                {
                    pdfFiles.AddRange(Directory.GetFiles(accountDataDir, "*.pdf", SearchOption.AllDirectories));
                }
            }
            if (pdfFiles.Count == 0)
            {
                Console.WriteLine("No PDF files found in AccountData.");
                return;
            }
            Console.WriteLine($"Found {pdfFiles.Count} PDF files.");
            int totalImported = 0;
            int totalProcessed = 0;
            foreach (var pdfPath in pdfFiles)
            {
                var txtPath = Path.ChangeExtension(pdfPath, ".txt");
                if (!force && File.Exists(txtPath))
                {
                    Console.WriteLine($"Skipping {Path.GetFileName(pdfPath)} (TXT exists)");
                    continue;
                }
                Console.WriteLine($"Processing {Path.GetFileName(pdfPath)}...");
                try
                {
                    using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
                    var fileName = Path.GetFileName(pdfPath);
                    var pdfParser = new PdfStatementParser(parserLogger);
                    var parsers = new[] { pdfParser };
                    var parsingService = new BankStatementParsingService(parsers, serviceLogger);
                    var result = await parsingService.ParseStatementAsync(fileStream, fileName, "Bank");
                    using (var db = new BankStatementParsingContext())
                    {
                        var importService = new BankStatementImportService(db, importLogger);
                        var importedCount = importService.ImportStatement(result);
                        totalImported += importedCount;
                        totalProcessed++;
                        Console.WriteLine($"Imported {importedCount} transactions from {fileName}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing {pdfPath}: {ex.Message}");
                }
            }
            Console.WriteLine($"\nProcessed {totalProcessed} PDFs. Imported {totalImported} transactions in total.");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
