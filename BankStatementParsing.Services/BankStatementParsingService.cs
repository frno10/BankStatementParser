using BankStatementParsing.Core.Interfaces;
using BankStatementParsing.Core.Models;
using Microsoft.Extensions.Logging;

namespace BankStatementParsing.Services;

public class BankStatementParsingService
{
    private readonly IEnumerable<IFileParserService> _parsers;
    private readonly ILogger<BankStatementParsingService> _logger;

    public BankStatementParsingService(
        IEnumerable<IFileParserService> parsers,
        ILogger<BankStatementParsingService> logger)
    {
        _parsers = parsers;
        _logger = logger;
    }

    public async Task<BankStatementData> ParseStatementAsync(Stream fileStream, string fileName, string bankName)
    {
        _logger.LogInformation("Starting to parse bank statement: {FileName}, Bank: {BankName}", fileName, bankName);

        var parser = FindParser(fileName, bankName);
        if (parser == null)
        {
            var supportedBanks = string.Join(", ", _parsers.Select(p => p.SupportedBankName));
            throw new NotSupportedException($"No parser found for bank '{bankName}'. Supported banks: {supportedBanks}");
        }

        _logger.LogInformation("Using parser for bank: {SupportedBank}", parser.SupportedBankName);

        return await parser.ParseAsync(fileStream, fileName, bankName);
    }

    public bool CanParseStatement(string fileName, string bankName)
    {
        return FindParser(fileName, bankName) != null;
    }

    private IFileParserService? FindParser(string fileName, string bankName)
    {
        return _parsers.FirstOrDefault(p => p.CanParse(fileName, bankName));
    }

    public IEnumerable<string> GetSupportedBanks()
    {
        return _parsers.Select(p => p.SupportedBankName);
    }
} 