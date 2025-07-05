using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

namespace BankStatementParsing.CLI.Commands;

public abstract class BaseCommand : Command
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILogger Logger;
    
    // Store option references to avoid fragile name-based lookups
    private readonly Option<bool> _verboseOption;
    private readonly Option<bool> _quietOption;
    private readonly Option<string?> _outputOption;

    protected BaseCommand(string name, string description, IServiceProvider serviceProvider) 
        : base(name, description)
    {
        ServiceProvider = serviceProvider;
        Logger = serviceProvider.GetRequiredService<ILogger<BaseCommand>>();
        
        // Create and store option references
        _verboseOption = new Option<bool>("--verbose", "Enable verbose output");
        _quietOption = new Option<bool>("--quiet", "Suppress non-error output");
        _outputOption = new Option<string?>("--output", "Output format (json, table, csv)") { ArgumentHelpName = "format" };
        
        // Add common options
        AddOption(_verboseOption);
        AddOption(_quietOption);
        AddOption(_outputOption);
    }

    protected bool IsVerbose(InvocationContext context) => 
        context.ParseResult.GetValueForOption(_verboseOption);

    protected bool IsQuiet(InvocationContext context) => 
        context.ParseResult.GetValueForOption(_quietOption);

    protected string GetOutputFormat(InvocationContext context) => 
        context.ParseResult.GetValueForOption(_outputOption) ?? "table";

    protected void WriteOutput(string message, InvocationContext context)
    {
        if (!IsQuiet(context))
        {
            Console.WriteLine(message);
        }
    }

    protected void WriteVerbose(string message, InvocationContext context)
    {
        if (IsVerbose(context))
        {
            Console.WriteLine($"[VERBOSE] {message}");
        }
    }

    protected void WriteError(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
        Logger.LogError(message);
    }
}