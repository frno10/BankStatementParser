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

        // Handle Russian number format (space as thousand separator, comma as decimal)
        amountText = amountText.Trim()
            .Replace("₽", "")
            .Replace("RUB", "")
            .Replace("руб", "")
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

        // Common Russian date formats
        var formats = new[] 
        { 
            "dd.MM.yyyy", 
            "dd/MM/yyyy", 
            "yyyy-MM-dd", 
            "dd-MM-yyyy",
            "dd.MM.yy",
            "dd/MM/yy"
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
        // Common patterns for Russian bank account numbers
        var patterns = new[]
        {
            @"\b\d{20}\b", // 20-digit account number
            @"Счет[:\s]*(\d{20})", // "Счет: 12345678901234567890"
            @"Account[:\s]*(\d{20})", // "Account: 12345678901234567890"
            @"р/с[:\s]*(\d{20})", // "р/с: 12345678901234567890"
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
        using var document = PdfDocument.Open(fileStream);
        var allText = string.Empty;
        for (int pageNum = 1; pageNum <= document.NumberOfPages; pageNum++)
        {
            var page = document.GetPage(pageNum);
            allText += page.Text + "\n";
        }
        await File.WriteAllTextAsync(txtFilePath, allText);
        _logger.LogInformation("PDF text extraction complete: {TxtFilePath}", txtFilePath);
        // Reset stream position for further reading
        if (fileStream.CanSeek)
            fileStream.Position = 0;
        return txtFilePath;
    }
} 