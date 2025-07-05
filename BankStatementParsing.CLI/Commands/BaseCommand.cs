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

    protected BaseCommand(string name, string description, IServiceProvider serviceProvider) 
        : base(name, description)
    {
        ServiceProvider = serviceProvider;
        Logger = serviceProvider.GetRequiredService<ILogger<BaseCommand>>();
        
        // Add common options
        AddOption(new Option<bool>("--verbose", "Enable verbose output"));
        AddOption(new Option<bool>("--quiet", "Suppress non-error output"));
        AddOption(new Option<string?>("--output", "Output format (json, table, csv)") { ArgumentHelpName = "format" });
    }

    protected bool IsVerbose(InvocationContext context) => 
        context.ParseResult.GetValueForOption(GetOption("--verbose")) as bool? ?? false;

    protected bool IsQuiet(InvocationContext context) => 
        context.ParseResult.GetValueForOption(GetOption("--quiet")) as bool? ?? false;

    protected string GetOutputFormat(InvocationContext context) => 
        context.ParseResult.GetValueForOption(GetOption("--output")) as string ?? "table";

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

    private Option GetOption(string name)
    {
        return Options.First(o => o.Name == name.TrimStart('-'));
    }
}