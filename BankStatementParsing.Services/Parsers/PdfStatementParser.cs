using BankStatementParsing.Core.Models;
using BankStatementParsing.Core.Enums;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using System.Text.RegularExpressions;

namespace BankStatementParsing.Services.Parsers;

public class PdfStatementParser : BaseBankStatementParser
{
    public override string SupportedBankName => "Bank";

    public PdfStatementParser(ILogger<PdfStatementParser> logger) : base(logger) { }

    public override bool CanParse(string fileName, string bankName)
    {
        return Path.GetExtension(fileName).ToLower() == ".pdf" && 
               (bankName.ToUpper().Contains("MSKB") || fileName.ToUpper().Contains("MSKB"));
    }

    public override async Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName)
    {
        try
        {
            _logger.LogInformation("Starting to parse MSKB PDF: {FileName}", fileName);

            // Always resolve the full path to the PDF for text extraction
            string pdfFilePath = fileName;
            if (!Path.IsPathRooted(pdfFilePath))
            {
                // Try to find the PDF in the AccountData directory if not rooted
                var possiblePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                if (File.Exists(possiblePath))
                    pdfFilePath = possiblePath;
                else
                {
                    // Search AccountData for the file
                    var solutionDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
                    if (solutionDir != null)
                    {
                        var accountDataDir = Path.Combine(solutionDir, "AccountData");
                        if (Directory.Exists(accountDataDir))
                        {
                            var found = Directory.GetFiles(accountDataDir, Path.GetFileName(fileName), SearchOption.AllDirectories).FirstOrDefault();
                            if (found != null)
                                pdfFilePath = found;
                        }
                    }
                }
            }
            await ExtractTextToFileIfNeededAsync(fileStream, pdfFilePath);

            var statementData = new BankStatementData
            {
                BankName = "Moscow Bank (MSKB)",
                Currency = "RUB"
            };

            using var document = PdfDocument.Open(fileStream);
            var allText = string.Empty;
            var transactions = new List<TransactionData>();

            // Extract text from all pages
            for (int pageNum = 1; pageNum <= document.NumberOfPages; pageNum++)
            {
                var page = document.GetPage(pageNum);
                var pageText = page.Text;
                allText += pageText + "\n";
                
                _logger.LogDebug("Page {PageNum} text length: {Length}", pageNum, pageText.Length);
            }

            _logger.LogDebug("Total extracted text length: {Length}", allText.Length);
            _logger.LogDebug("First 500 characters: {Text}", allText.Length > 500 ? allText.Substring(0, 500) : allText);

            // Parse account information
            await ParseAccountInformation(statementData, allText);
            
            // Parse statement period
            await ParseStatementPeriod(statementData, allText);
            
            // Parse balances
            await ParseBalances(statementData, allText);
            
            // Parse transactions
            statementData.Transactions = await ParseTransactions(allText);

            _logger.LogInformation("Successfully parsed MSKB statement. Found {TransactionCount} transactions", 
                statementData.Transactions.Count);

            return statementData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse MSKB PDF: {FileName}", fileName);
            throw;
        }
    }

    private async Task ParseAccountInformation(BankStatementData statementData, string text)
    {
        // Extract account number
        statementData.AccountNumber = ExtractAccountNumber(text);
        
        // Extract account holder name - look for common patterns
        var namePatterns = new[]
        {
            @"Владелец\s*счета[:\s]*([^\n\r]+)",
            @"Account\s*holder[:\s]*([^\n\r]+)",
            @"Клиент[:\s]*([^\n\r]+)",
            @"ФИО[:\s]*([^\n\r]+)"
        };

        foreach (var pattern in namePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                statementData.AccountHolderName = match.Groups[1].Value.Trim();
                break;
            }
        }

        _logger.LogDebug("Parsed account info - Number: {AccountNumber}, Holder: {Holder}", 
            statementData.AccountNumber, statementData.AccountHolderName);
    }

    private async Task ParseStatementPeriod(BankStatementData statementData, string text)
    {
        // Look for period patterns
        var periodPatterns = new[]
        {
            @"период\s*с\s*(\d{2}\.\d{2}\.\d{4})\s*по\s*(\d{2}\.\d{2}\.\d{4})",
            @"period\s*from\s*(\d{2}\.\d{2}\.\d{4})\s*to\s*(\d{2}\.\d{2}\.\d{4})",
            @"за\s*период\s*(\d{2}\.\d{2}\.\d{4})\s*-\s*(\d{2}\.\d{2}\.\d{4})",
            @"(\d{2}\.\d{2}\.\d{4})\s*-\s*(\d{2}\.\d{2}\.\d{4})"
        };

        foreach (var pattern in periodPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 2)
            {
                statementData.StatementPeriodStart = ParseDate(match.Groups[1].Value);
                statementData.StatementPeriodEnd = ParseDate(match.Groups[2].Value);
                break;
            }
        }

        _logger.LogDebug("Parsed period - Start: {Start}, End: {End}", 
            statementData.StatementPeriodStart, statementData.StatementPeriodEnd);
    }

    private async Task ParseBalances(BankStatementData statementData, string text)
    {
        // Look for balance patterns
        var openingBalancePatterns = new[]
        {
            @"остаток\s*на\s*начало[:\s]*([0-9\s,.-]+)",
            @"opening\s*balance[:\s]*([0-9\s,.-]+)",
            @"входящий\s*остаток[:\s]*([0-9\s,.-]+)",
            @"сальдо\s*на\s*начало[:\s]*([0-9\s,.-]+)"
        };

        var closingBalancePatterns = new[]
        {
            @"остаток\s*на\s*конец[:\s]*([0-9\s,.-]+)",
            @"closing\s*balance[:\s]*([0-9\s,.-]+)",
            @"исходящий\s*остаток[:\s]*([0-9\s,.-]+)",
            @"сальдо\s*на\s*конец[:\s]*([0-9\s,.-]+)"
        };

        foreach (var pattern in openingBalancePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                statementData.OpeningBalance = ParseAmount(match.Groups[1].Value);
                break;
            }
        }

        foreach (var pattern in closingBalancePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                statementData.ClosingBalance = ParseAmount(match.Groups[1].Value);
                break;
            }
        }

        _logger.LogDebug("Parsed balances - Opening: {Opening}, Closing: {Closing}", 
            statementData.OpeningBalance, statementData.ClosingBalance);
    }

    private async Task<List<TransactionData>> ParseTransactions(string text)
    {
        var transactions = new List<TransactionData>();
        
        // Split text into lines for transaction parsing
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Look for transaction patterns - this may need to be adjusted based on actual MSKB format
        var transactionPattern = @"(\d{2}\.\d{2}\.\d{4})\s+([^0-9-]+)\s+([-]?[0-9\s,.-]+)";
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var match = Regex.Match(line, transactionPattern);
            if (match.Success && match.Groups.Count > 3)
            {
                try
                {
                    var transaction = new TransactionData
                    {
                        Date = ParseDate(match.Groups[1].Value),
                        Description = match.Groups[2].Value.Trim(),
                        Amount = ParseAmount(match.Groups[3].Value)
                    };

                    // Determine transaction type after amount adjustment
                    // Make amount negative for debit transactions first if needed
                    if (transaction.Amount > 0 && ShouldBeDebit(transaction.Description))
                    {
                        transaction.Amount = -transaction.Amount;
                    }

                    transaction.Type = DetermineTransactionType(transaction.Amount, transaction.Description);

                    transactions.Add(transaction);

                    _logger.LogDebug("Parsed transaction: {Date} | {Description} | {Amount}", 
                        transaction.Date, transaction.Description, transaction.Amount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse transaction line: {Line}", line);
                }
            }
        }

        _logger.LogInformation("Found {Count} transactions", transactions.Count);
        return transactions;
    }

    private bool ShouldBeDebit(string description)
    {
        // Helper method to determine if a transaction should be a debit based on description
        var debitKeywords = new[] { "списание", "платеж", "оплата", "комиссия", "снятие", "withdrawal", "payment", "fee" };
        return debitKeywords.Any(keyword => description.ToLower().Contains(keyword.ToLower()));
    }
} 