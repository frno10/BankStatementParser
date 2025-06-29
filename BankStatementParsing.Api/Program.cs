using BankStatementParsing.Api;
using BankStatementParsing.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Compute absolute path to the shared database
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
var absoluteDbPath = Path.Combine(solutionRoot, "Database", "bankstatements.db");
var connectionString = $"Data Source={absoluteDbPath}";
builder.Services.AddDbContext<BankStatementParsingContext>(options =>
    options.UseSqlite(connectionString));
Console.WriteLine($"[DEBUG] Using SQLite connection string: {connectionString}");
Console.WriteLine($"[DEBUG] Absolute path to SQLite DB: {absoluteDbPath}");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

// Add endpoint to test PDF parsing
app.MapGet("/test-pdf-parse", async () =>
{
    try 
    {
        var result = await TestPdfParser.RunAsync(Array.Empty<string>());
        return result == 0 ? Results.Ok("PDF parsing completed successfully") : Results.Problem("PDF parsing failed");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
