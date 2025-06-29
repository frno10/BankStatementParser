using BankStatementParsing.Core.Models;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace BankStatementParsing.Services.Parsers
{
    public class JsonDrivenBankStatementParser
    {
        private readonly List<JsonBankParserDefinition> _definitions;

        public JsonDrivenBankStatementParser(List<JsonBankParserDefinition> definitions)
        {
            _definitions = definitions;
        }

        public BankStatementData? Parse(string text)
        {
            var def = FindMatchingDefinition(text);
            if (def == null)
                return null;

            var statement = new BankStatementData
            {
                BankName = def.BankName
            };

            // Account info
            if (!string.IsNullOrEmpty(def.AccountInfo.AccountNumber))
                statement.AccountNumber = MatchSingle(def.AccountInfo.AccountNumber, text);
            if (!string.IsNullOrEmpty(def.AccountInfo.AccountHolder))
                statement.AccountHolderName = MatchSingle(def.AccountInfo.AccountHolder, text);

            // Period
            if (!string.IsNullOrEmpty(def.Period.Start))
                statement.StatementPeriodStart = ParseDate(MatchSingle(def.Period.Start, text));
            if (!string.IsNullOrEmpty(def.Period.End))
                statement.StatementPeriodEnd = ParseDate(MatchSingle(def.Period.End, text));

            // Transactions
            statement.Transactions = ParseTransactions(def.Transaction, text);

            return statement;
        }

        private JsonBankParserDefinition? FindMatchingDefinition(string text)
        {
            foreach (var def in _definitions)
            {
                if (def.Recognition.Keywords.Any(k => text.Contains(k)))
                    return def;
            }
            return null;
        }

        private string? MatchSingle(string pattern, string text)
        {
            var regex = new Regex(pattern, RegexOptions.Multiline);
            var match = regex.Match(text);
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : null;
        }

        private List<TransactionData> ParseTransactions(TransactionDefinition def, string text)
        {
            var transactions = new List<TransactionData>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var mainRegex = new Regex(def.MainPattern.Trim(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                var match = mainRegex.Match(trimmedLine);
                
                if (match.Success)
                {
                    var current = new TransactionData();
                    
                    // Extract main transaction fields
                    foreach (var field in def.Fields)
                    {
                        var value = match.Groups.Count > field.Value ? match.Groups[field.Value].Value.Trim() : null;
                        switch (field.Key)
                        {
                            case "date":
                                current.Date = ParseDate(value);
                                break;
                            case "description":
                                current.Description = value;
                                break;
                            case "amount":
                                current.Amount = ParseAmount(value);
                                break;
                        }
                    }
                    
                    transactions.Add(current);
                }
            }
            return transactions;
        }

        private static decimal ParseAmount(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            value = value.Replace(" ", "").Replace(",", ".");
            var match = Regex.Match(value, "-?\\d+(\\.\\d+)?");
            return match.Success ? decimal.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }

        private static DateTime ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.MinValue;
            var formats = new[] { "d. M.", "d. M. yyyy", "dd. MM. yyyy", "d.M.yyyy", "d.M." };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(value.Trim(), fmt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                    return dt;
            }
            DateTime.TryParse(value, out var fallback);
            return fallback;
        }
    }
} 