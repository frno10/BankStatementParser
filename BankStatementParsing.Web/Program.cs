using BankStatementParsing.Infrastructure;
using BankStatementParsing.Services;
using BankStatementParsing.Web.Hubs;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Initialize Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/bankstatement-web-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Set a unique console window title for easy identification
Console.Title = "BankStatementParsing.Web (ASP.NET Core)";

// Log the process ID for Task Manager identification
Console.WriteLine($"[DEBUG] Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");

// Only configure SQLite database if not in testing environment
if (!builder.Environment.IsEnvironment("Testing"))
{
    // Compute absolute path to the shared database
    var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    var absoluteDbPath = Path.Combine(solutionRoot, "Database", "bankstatements.db");
    var connectionString = $"Data Source={absoluteDbPath}";
    builder.Services.AddDbContext<BankStatementParsingContext>(options =>
        options.UseSqlite(connectionString)
        .LogTo(message => { }, LogLevel.Warning)); // Disable SQL command logging

    // Log the connection string and absolute path at startup
    Console.WriteLine($"[DEBUG] Using SQLite connection string: {connectionString}");
    Console.WriteLine($"[DEBUG] Absolute path to SQLite DB: {absoluteDbPath}");
}

// Register new services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITransactionRuleService, TransactionRuleService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddHostedService<SchedulerService>();

// Configure SignalR
builder.Services.AddSignalR();

// Configure Serilog
builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Map SignalR hub
app.MapHub<ProcessingHub>("/processingHub");

app.Run();

// Make Program class accessible for testing
public partial class Program { }
