using BankStatementParsing.Core.Models;
using BankStatementParsing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using OfficeOpenXml;
using System.Globalization;
using System.Linq;

namespace BankStatementParsing.Services;

public interface IExportService
{
    Task<ExportRequest> CreateExportRequestAsync(ExportRequest request);
    Task<ExportRequest> ProcessExportRequestAsync(int requestId);
    Task<List<ExportRequest>> GetExportRequestsAsync(int userId);
    Task<byte[]> GetExportFileAsync(int requestId);
    Task<string> ExportToCsvAsync(IEnumerable<Transaction> transactions);
    Task<byte[]> ExportToExcelAsync(IEnumerable<Transaction> transactions);
    Task<string> ExportToQifAsync(IEnumerable<Transaction> transactions);
    Task<string> ExportToOfxAsync(IEnumerable<Transaction> transactions);
    Task<string> ExportToJsonAsync(IEnumerable<Transaction> transactions);
}

public class ExportService : IExportService
{
    private readonly BankStatementParsingContext _context;
    private readonly ILogger<ExportService> _logger;
    private readonly string _exportsDirectory;

    public ExportService(
        BankStatementParsingContext context,
        ILogger<ExportService> logger)
    {
        _context = context;
        _logger = logger;
        _exportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "exports");
        Directory.CreateDirectory(_exportsDirectory);
    }

    public async Task<ExportRequest> CreateExportRequestAsync(ExportRequest request)
    {
        _context.ExportRequests.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<ExportRequest> ProcessExportRequestAsync(int requestId)
    {
        var request = await _context.ExportRequests.FindAsync(requestId);
        if (request == null)
            throw new ArgumentException("Export request not found");

        try
        {
            request.Status = "Processing";
            await _context.SaveChangesAsync();

            var transactions = await GetFilteredTransactionsAsync(request);
            var fileName = $"export_{requestId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            
            byte[] fileData;
            string fileExtension;

            switch (request.Format.ToLower())
            {
                case "csv":
                    var csvData = await ExportToCsvAsync(transactions);
                    fileData = Encoding.UTF8.GetBytes(csvData);
                    fileExtension = ".csv";
                    break;
                case "excel":
                    fileData = await ExportToExcelAsync(transactions);
                    fileExtension = ".xlsx";
                    break;
                case "qif":
                    var qifData = await ExportToQifAsync(transactions);
                    fileData = Encoding.UTF8.GetBytes(qifData);
                    fileExtension = ".qif";
                    break;
                case "ofx":
                    var ofxData = await ExportToOfxAsync(transactions);
                    fileData = Encoding.UTF8.GetBytes(ofxData);
                    fileExtension = ".ofx";
                    break;
                case "json":
                    var jsonData = await ExportToJsonAsync(transactions);
                    fileData = Encoding.UTF8.GetBytes(jsonData);
                    fileExtension = ".json";
                    break;
                default:
                    throw new ArgumentException($"Unsupported format: {request.Format}");
            }

            var filePath = Path.Combine(_exportsDirectory, fileName + fileExtension);
            await File.WriteAllBytesAsync(filePath, fileData);

            request.Status = "Completed";
            request.FilePath = filePath;
            request.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Export completed for request {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed for request {RequestId}", requestId);
            request.Status = "Failed";
            request.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync();
        }

        return request;
    }

    public async Task<List<ExportRequest>> GetExportRequestsAsync(int userId)
    {
        return await _context.ExportRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<byte[]> GetExportFileAsync(int requestId)
    {
        var request = await _context.ExportRequests.FindAsync(requestId);
        if (request == null || string.IsNullOrEmpty(request.FilePath))
            throw new ArgumentException("Export file not found");

        if (!File.Exists(request.FilePath))
            throw new FileNotFoundException("Export file not found on disk");

        return await File.ReadAllBytesAsync(request.FilePath);
    }

    public async Task<string> ExportToCsvAsync(IEnumerable<Transaction> transactions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Description,Amount,Currency,Reference,Merchant,Category,Tags");

        foreach (var transaction in transactions)
        {
            var tags = transaction.TransactionTags?.Select(tt => tt.Tag?.Name).Where(t => !string.IsNullOrEmpty(t));
            var tagsString = tags != null ? string.Join(";", tags) : "";
            
            sb.AppendLine($"{transaction.Date:yyyy-MM-dd}," +
                         $"\"{transaction.Description ?? ""}\"," +
                         $"{transaction.Amount}," +
                         $"{transaction.Currency ?? ""}," +
                         $"\"{transaction.Reference ?? ""}\"," +
                         $"\"{transaction.Merchant?.Name ?? ""}\"," +
                         $"\"\"," + // Category placeholder
                         $"\"{tagsString}\"");
        }

        return sb.ToString();
    }

    public async Task<byte[]> ExportToExcelAsync(IEnumerable<Transaction> transactions)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Transactions");

        // Headers
        worksheet.Cells[1, 1].Value = "Date";
        worksheet.Cells[1, 2].Value = "Description";
        worksheet.Cells[1, 3].Value = "Amount";
        worksheet.Cells[1, 4].Value = "Currency";
        worksheet.Cells[1, 5].Value = "Reference";
        worksheet.Cells[1, 6].Value = "Merchant";
        worksheet.Cells[1, 7].Value = "Category";
        worksheet.Cells[1, 8].Value = "Tags";

        // Data
        var row = 2;
        foreach (var transaction in transactions)
        {
            var tags = transaction.TransactionTags?.Select(tt => tt.Tag?.Name).Where(t => !string.IsNullOrEmpty(t));
            var tagsString = tags != null ? string.Join(";", tags) : "";

            worksheet.Cells[row, 1].Value = transaction.Date;
            worksheet.Cells[row, 2].Value = transaction.Description ?? "";
            worksheet.Cells[row, 3].Value = transaction.Amount;
            worksheet.Cells[row, 4].Value = transaction.Currency ?? "";
            worksheet.Cells[row, 5].Value = transaction.Reference ?? "";
            worksheet.Cells[row, 6].Value = transaction.Merchant?.Name ?? "";
            worksheet.Cells[row, 7].Value = ""; // Category placeholder
            worksheet.Cells[row, 8].Value = tagsString;
            row++;
        }

        // Format headers
        using var range = worksheet.Cells[1, 1, 1, 8];
        range.Style.Font.Bold = true;
        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    public async Task<string> ExportToQifAsync(IEnumerable<Transaction> transactions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("!Type:Bank");

        foreach (var transaction in transactions)
        {
            sb.AppendLine($"D{transaction.Date:M/d/yyyy}");
            sb.AppendLine($"T{transaction.Amount}");
            sb.AppendLine($"P{transaction.Merchant?.Name ?? transaction.Description ?? ""}");
            sb.AppendLine($"M{transaction.Description ?? ""}");
            if (!string.IsNullOrEmpty(transaction.Reference))
                sb.AppendLine($"#{transaction.Reference}");
            sb.AppendLine("^");
        }

        return sb.ToString();
    }

    public async Task<string> ExportToOfxAsync(IEnumerable<Transaction> transactions)
    {
        var txList = transactions.ToList();
        if (!txList.Any())
        {
            _logger.LogWarning("ExportToOfxAsync called with empty transaction list");
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("OFXHEADER:100");
        sb.AppendLine("DATA:OFXSGML");
        sb.AppendLine("VERSION:102");
        sb.AppendLine("SECURITY:NONE");
        sb.AppendLine("ENCODING:USASCII");
        sb.AppendLine("CHARSET:1252");
        sb.AppendLine("COMPRESSION:NONE");
        sb.AppendLine("OLDFILEUID:NONE");
        sb.AppendLine("NEWFILEUID:NONE");
        sb.AppendLine();
        sb.AppendLine("<OFX>");
        sb.AppendLine("<SIGNONMSGSRSV1>");
        sb.AppendLine("<SONRS>");
        sb.AppendLine("<STATUS>");
        sb.AppendLine("<CODE>0");
        sb.AppendLine("<SEVERITY>INFO");
        sb.AppendLine("</STATUS>");
        sb.AppendLine("<DTSERVER>" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        sb.AppendLine("<LANGUAGE>ENG");
        sb.AppendLine("</SONRS>");
        sb.AppendLine("</SIGNONMSGSRSV1>");
        sb.AppendLine("<BANKMSGSRSV1>");
        sb.AppendLine("<STMTTRNRS>");
        sb.AppendLine("<TRNUID>1");
        sb.AppendLine("<STATUS>");
        sb.AppendLine("<CODE>0");
        sb.AppendLine("<SEVERITY>INFO");
        sb.AppendLine("</STATUS>");
        sb.AppendLine("<STMTRS>");
        sb.AppendLine("<CURDEF>USD");
        sb.AppendLine("<BANKACCTFROM>");
        sb.AppendLine("<BANKID>123456789");
        sb.AppendLine("<ACCTID>123456789");
        sb.AppendLine("<ACCTTYPE>CHECKING");
        sb.AppendLine("</BANKACCTFROM>");
        sb.AppendLine("<BANKTRANLIST>");
        sb.AppendLine("<DTSTART>" + txList.Min(t => t.Date).ToString("yyyyMMdd"));
        sb.AppendLine("<DTEND>" + txList.Max(t => t.Date).ToString("yyyyMMdd"));

        foreach (var transaction in txList)
        {
            sb.AppendLine("<STMTTRN>");
            sb.AppendLine("<TRNTYPE>" + (transaction.Amount > 0 ? "CREDIT" : "DEBIT"));
            sb.AppendLine("<DTPOSTED>" + transaction.Date.ToString("yyyyMMdd"));
            sb.AppendLine("<TRNAMT>" + transaction.Amount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("<FITID>" + transaction.Id);
            sb.AppendLine("<NAME>" + (transaction.Merchant?.Name ?? transaction.Description ?? ""));
            sb.AppendLine("<MEMO>" + (transaction.Description ?? ""));
            sb.AppendLine("</STMTTRN>");
        }

        sb.AppendLine("</BANKTRANLIST>");
        sb.AppendLine("</STMTRS>");
        sb.AppendLine("</STMTTRNRS>");
        sb.AppendLine("</BANKMSGSRSV1>");
        sb.AppendLine("</OFX>");

        return sb.ToString();
    }

    public async Task<string> ExportToJsonAsync(IEnumerable<Transaction> transactions)
    {
        var exportData = transactions.Select(t => new
        {
            t.Id,
            t.Date,
            t.Description,
            t.Amount,
            t.Currency,
            t.Reference,
            Merchant = t.Merchant?.Name,
            Tags = t.TransactionTags?.Select(tt => tt.Tag?.Name).Where(name => !string.IsNullOrEmpty(name)).ToList()
        });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(exportData, options);
    }

    private async Task<List<Transaction>> GetFilteredTransactionsAsync(ExportRequest request)
    {
        var query = _context.Transactions
            .Include(t => t.Merchant)
            .Include(t => t.TransactionTags)
            .ThenInclude(tt => tt.Tag)
            .Include(t => t.Statement)
            .ThenInclude(s => s.Account)
            .AsQueryable();

        // Apply filters
        if (request.DateFrom.HasValue)
            query = query.Where(t => t.Date >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(t => t.Date <= request.DateTo.Value);

        if (!string.IsNullOrEmpty(request.AccountFilter))
            query = query.Where(t => t.Statement.Account.AccountNumber.Contains(request.AccountFilter));

        if (!string.IsNullOrEmpty(request.MerchantFilter))
            query = query.Where(t => t.Merchant != null && t.Merchant.Name.Contains(request.MerchantFilter));

        if (!string.IsNullOrEmpty(request.TagFilter))
            query = query.Where(t => t.TransactionTags.Any(tt => tt.Tag.Name.Contains(request.TagFilter)));

        if (request.AmountMin.HasValue)
            query = query.Where(t => t.Amount >= (double)request.AmountMin.Value);

        if (request.AmountMax.HasValue)
            query = query.Where(t => t.Amount <= (double)request.AmountMax.Value);

        return await query.OrderBy(t => t.Date).ToListAsync();
    }
}