using BankStatementParsing.Core.Interfaces;
using BankStatementParsing.Core.Models;
using Microsoft.Extensions.Logging;
using BankStatementParsing.Services.Parsers;
using System.IO;

namespace BankStatementParsing.Services;

public class BankStatementParsingService
{
    private readonly IEnumerable<IFileParserService> _parsers;
    private readonly ILogger<BankStatementParsingService> _logger;
    private readonly JsonDrivenBankStatementParser? _jsonParser;

    public BankStatementParsingService(
        IEnumerable<IFileParserService> parsers,
        ILogger<BankStatementParsingService> logger)
    {
        _parsers = parsers;
        _logger = logger;
        // Load JSON definitions at startup
        // Find the definitions directory relative to the assembly location
        var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        var solutionRoot = assemblyDir;
        
        // Navigate up from bin/Debug/net9.0 to find the solution root
        for (int i = 0; i < 5 && solutionRoot != null; i++)
        {
            solutionRoot = Path.GetDirectoryName(solutionRoot);
            if (solutionRoot != null && File.Exists(Path.Combine(solutionRoot, "BankStatementParsing.sln")))
                break;
        }
        
        var defDir = solutionRoot != null 
            ? Path.Combine(solutionRoot, "BankStatementParsing.Services", "Parsers", "Definitions")
            : Path.Combine(Directory.GetCurrentDirectory(), "BankStatementParsing.Services", "Parsers", "Definitions");
            
        _logger.LogInformation($"[JSON Parser] Looking for definitions in: {defDir}");
        if (Directory.Exists(defDir))
        {
            var defs = JsonBankParserLoader.LoadAll(defDir);
            if (defs.Count > 0)
            {
                _jsonParser = new JsonDrivenBankStatementParser(defs);
                _logger.LogInformation($"[JSON Parser] Loaded {defs.Count} parser definitions");
            }
            else
            {
                _logger.LogWarning("[JSON Parser] No valid definitions found in directory");
            }
        }
        else
        {
            _logger.LogWarning($"[JSON Parser] Definitions directory not found: {defDir}");
        }
    }

    public async Task<BankStatementData> ParseStatementAsync(Stream fileStream, string fileName, string bankName)
    {
        _logger.LogInformation("Starting to parse bank statement: {FileName}, Bank: {BankName}", fileName, bankName);

        // Try JSON-driven parser first
        if (_jsonParser != null)
        {
            try
            {
                // Read all text from fileStream
                fileStream.Position = 0;
                using var reader = new StreamReader(fileStream, leaveOpen: true);
                var text = await reader.ReadToEndAsync();
                fileStream.Position = 0;
                
                _logger.LogInformation("[JSON Parser] Attempting to parse with JSON-driven parser...");
                var parsed = _jsonParser.Parse(text);
                if (parsed != null && parsed.Transactions.Count > 0)
                {
                    _logger.LogInformation("[JSON Parser] Successfully parsed using JSON-driven parser: {Count} transactions", parsed.Transactions.Count);
                    return parsed;
                }
                else if (parsed != null)
                {
                    _logger.LogWarning("[JSON Parser] JSON parser matched bank but found 0 transactions");
                }
                else
                {
                    _logger.LogInformation("[JSON Parser] No matching definition found for this statement");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JSON Parser] Error during JSON-driven parsing");
            }
        }
        else
        {
            _logger.LogInformation("[JSON Parser] JSON parser not available");
        }

        // Fallback to existing logic
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