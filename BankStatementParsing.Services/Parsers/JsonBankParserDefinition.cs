using System.Collections.Generic;

namespace BankStatementParsing.Services.Parsers
{
    public class JsonBankParserDefinition
    {
        public string BankName { get; set; } = string.Empty;
        public RecognitionDefinition Recognition { get; set; } = new();
        public AccountInfoDefinition AccountInfo { get; set; } = new();
        public PeriodDefinition Period { get; set; } = new();
        public TransactionDefinition Transaction { get; set; } = new();
    }

    public class RecognitionDefinition
    {
        public List<string> Keywords { get; set; } = new();
        public string? AccountNumberPattern { get; set; }
    }

    public class AccountInfoDefinition
    {
        public string? AccountNumber { get; set; }
        public string? AccountHolder { get; set; }
    }

    public class PeriodDefinition
    {
        public string? Start { get; set; }
        public string? End { get; set; }
    }

    public class TransactionDefinition
    {
        public string MainPattern { get; set; } = string.Empty;
        public Dictionary<string, int> Fields { get; set; } = new();
        public List<TransactionDetailDefinition> Details { get; set; } = new();
    }

    public class TransactionDetailDefinition
    {
        public string Label { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
    }
} 