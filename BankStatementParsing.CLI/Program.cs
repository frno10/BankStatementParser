using BankStatementParsing.CLI.Commands;
using BankStatementParsing.CLI.Services;
using BankStatementParsing.Infrastructure;
using BankStatementParsing.Services;
using BankStatementParsing.Services.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BankStatementParsing.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Setup Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();

            // Build the host
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, configuration);
                })
                .Build();

            // Create root command
            var rootCommand = new RootCommand("Bank Statement Processing CLI")
            {
                Description = "A CLI tool for processing bank statement PDFs with granular command chaining capabilities"
            };

            // Add global options
            rootCommand.AddGlobalOption(new Option<string?>("--config", "Configuration file path"));
            rootCommand.AddGlobalOption(new Option<LogLevel>("--log-level", () => LogLevel.Information, "Logging level"));

            // Register commands
            var serviceProvider = host.Services;
            rootCommand.AddCommand(new ExtractCommand(serviceProvider));
            rootCommand.AddCommand(new ParseCommand(serviceProvider));
            rootCommand.AddCommand(new ImportCommand(serviceProvider));
            rootCommand.AddCommand(new ProcessCommand(serviceProvider));
            rootCommand.AddCommand(new DatabaseCommand(serviceProvider));

            // Add version option
            rootCommand.AddOption(new Option<bool>("--version", "Show version information"));
            rootCommand.SetHandler((bool version) =>
            {
                if (version)
                {
                    var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    Console.WriteLine($"bankstmt CLI v{assemblyVersion}");
                    Console.WriteLine("Bank Statement Processing Tool");
                    return;
                }
            }, rootCommand.Options.OfType<Option<bool>>().First(o => o.Name == "version"));

            // Parse and execute
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Database configuration
        var solutionRoot = FindSolutionRoot();
        var databasePath = Path.Combine(solutionRoot, "Database", "bankstatements.db");
        var connectionString = $"Data Source={databasePath}";
        
        services.AddDbContext<BankStatementParsingContext>(options =>
            options.UseSqlite(connectionString));

        // Register existing services
        services.AddTransient<PdfStatementParser>();
        services.AddTransient<BankStatementParsingService>(sp =>
            new BankStatementParsingService(
                new[] { sp.GetRequiredService<PdfStatementParser>() },
                sp.GetRequiredService<ILogger<BankStatementParsingService>>()));
        services.AddTransient<BankStatementImportService>();

        // Register CLI services
        services.AddTransient<IExtractionService, ExtractionService>();
        services.AddTransient<IParsingService, ParsingService>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<IPipelineService, PipelineService>();
    }

    private static string FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var searchDir = currentDir;

        // Navigate up to find the solution root
        for (int i = 0; i < 10 && searchDir != null; i++)
        {
            if (File.Exists(Path.Combine(searchDir, "BankStatementParsing.sln")))
            {
                return searchDir;
            }
            searchDir = Path.GetDirectoryName(searchDir);
        }

        // Fallback to current directory
        return currentDir;
    }
}