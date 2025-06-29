using BankStatementParsing.Core.Models;

namespace BankStatementParsing.Core.Interfaces;

public interface IFileParserService
{
    Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName);
    bool CanParse(string fileName, string bankName);
    string SupportedBankName { get; }
} 