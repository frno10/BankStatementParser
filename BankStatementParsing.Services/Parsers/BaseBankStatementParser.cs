using BankStatementParsing.Core.Interfaces;
using BankStatementParsing.Core.Models;
using BankStatementParsing.Core.Enums;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace BankStatementParsing.Services.Parsers;

public abstract class BaseBankStatementParser : IFileParserService
{
    protected readonly ILogger<BaseBankStatementParser> _logger;

    protected BaseBankStatementParser(ILogger<BaseBankStatementParser> logger)
    {
        _logger = logger;
    }

    public abstract Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName);
    public abstract bool CanParse(string fileName, string bankName);
    public abstract string SupportedBankName { get; }

    protected virtual decimal ParseAmount(string amountText)
    {
        if (string.IsNullOrWhiteSpace(amountText))
            return 0;

        // Handle various currency symbols and formats
        amountText = amountText.Trim()
            .Replace("₽", "")
            .Replace("RUB", "")
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("USD", "")
            .Replace("EUR", "")
            .Replace("GBP", "")
            .Replace(".", "")
            .Trim();

        // Convert comma to dot for decimal parsing
        if (amountText.Contains(','))
        {
            var parts = amountText.Split(',');
            if (parts.Length == 2)
            {
                amountText = parts[0].Replace(" ", "") + "." + parts[1];
            }
        }
        else
        {
            amountText = amountText.Replace(" ", "");
        }

        return decimal.TryParse(amountText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ? amount : 0;
    }

    protected virtual DateTime ParseDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return DateTime.MinValue;

        // Common international date formats
        var formats = new[] 
        { 
            "dd.MM.yyyy", 
            "dd/MM/yyyy", 
            "MM/dd/yyyy",
            "yyyy-MM-dd", 
            "dd-MM-yyyy",
            "MM-dd-yyyy",
            "dd.MM.yy",
            "dd/MM/yy",
            "MM/dd/yy"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateText.Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;
        }

        if (DateTime.TryParse(dateText.Trim(), out var parsedDate))
            return parsedDate;

        return DateTime.MinValue;
    }

    protected virtual string ExtractAccountNumber(string text)
    {
        // Common patterns for international bank account numbers
        var patterns = new[]
        {
            @"\b\d{8,20}\b", // 8-20 digit account number (more flexible)
            @"Account[:\s]*(\d{8,20})", // "Account: 12345678901234567890"
            @"Acc[:\s]*(\d{8,20})", // "Acc: 12345678"
            @"A/C[:\s]*(\d{8,20})", // "A/C: 12345678"
            @"Account\s*Number[:\s]*(\d{8,20})", // "Account Number: 12345678"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            }
        }

        return string.Empty;
    }

    protected virtual TransactionType DetermineTransactionType(decimal amount, string description)
    {
        if (amount > 0)
            return TransactionType.Credit;
        else
            return TransactionType.Debit;
    }

    protected async Task<string> ExtractTextToFileIfNeededAsync(Stream fileStream, string pdfFilePath)
    {
        var txtFilePath = Path.ChangeExtension(pdfFilePath, ".txt");
        if (File.Exists(txtFilePath))
        {
            _logger.LogInformation("Text file already exists: {TxtFilePath}, skipping extraction.", txtFilePath);
            return txtFilePath;
        }

        // Reset stream position in case it was read before
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        _logger.LogInformation("Extracting text from PDF to: {TxtFilePath}", txtFilePath);
        using var document = UglyToad.PdfPig.PdfDocument.Open(fileStream);
        var sb = new System.Text.StringBuilder();
        for (int pageNum = 1; pageNum <= document.NumberOfPages; pageNum++)
        {
            var page = document.GetPage(pageNum);
            // Group words by Y position (line)
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
        await File.WriteAllTextAsync(txtFilePath, sb.ToString());
        _logger.LogInformation("PDF text extraction complete: {TxtFilePath}", txtFilePath);
        // Reset stream position for further reading
        if (fileStream.CanSeek)
            fileStream.Position = 0;
        return txtFilePath;
    }
} 