using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BankStatementParsing.Services.Parsers
{
    public static class JsonBankParserLoader
    {
        public static List<JsonBankParserDefinition> LoadAll(string definitionsDirectory)
        {
            var definitions = new List<JsonBankParserDefinition>();
            if (!Directory.Exists(definitionsDirectory))
                return definitions;

            foreach (var file in Directory.GetFiles(definitionsDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    };
                    var def = JsonSerializer.Deserialize<JsonBankParserDefinition>(json, options);
                    if (def != null)
                        definitions.Add(def);
                }
                catch { /* log or ignore invalid files */ }
            }
            return definitions;
        }
    }
} 