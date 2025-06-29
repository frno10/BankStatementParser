# Logging Strategy - Serilog Implementation

## Overview
The application implements comprehensive logging using Serilog with multiple outputs including console, files, and structured JSON logging for analysis and monitoring.

## Logging Architecture

### Log Levels
- **Verbose**: Detailed tracing information
- **Debug**: Internal system events for debugging
- **Information**: General application flow
- **Warning**: Unexpected situations that don't halt execution
- **Error**: Error events that might still allow the application to continue
- **Fatal**: Very severe errors that might abort the application

### Log Categories
1. **Application Logs**: General application events and flows
2. **Processing Logs**: Detailed file processing information
3. **Error Logs**: Exceptions and error conditions
4. **Performance Logs**: Timing and performance metrics
5. **Security Logs**: Authentication and authorization events

## Serilog Configuration

### Startup Configuration
```csharp
public static class LoggingConfiguration
{
    public static IServiceCollection AddComprehensiveLogging(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentUserName()
            .Enrich.With<ProcessingContextEnricher>()
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.File(
                path: "Logs/Application/app-.log",
                formatter: new CompactJsonFormatter(),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true)
            .WriteTo.File(
                path: "Logs/Errors/errors-.log",
                restrictedToMinimumLevel: LogEventLevel.Error,
                formatter: new CompactJsonFormatter(),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90)
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("ProcessingSessionId"))
                .WriteTo.File(
                    path: "Logs/Processing/processing-.log",
                    formatter: new CompactJsonFormatter(),
                    rollingInterval: RollingInterval.Hour,
                    retainedFileCountLimit: 168)) // 7 days of hourly logs
            .CreateLogger();

        Log.Logger = loggerConfig;
        services.AddSerilog(loggerConfig);
        
        return services;
    }
}
```

### appsettings.json Configuration
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning",
        "BankStatementParsing.Services.FileProcessing": "Debug",
        "BankStatementParsing.Services.Parsing": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
          "restrictedToMinimumLevel": "Information"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/Application/app-.log",
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "shared": true
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithProcessId", "WithThreadId"],
    "Properties": {
      "Application": "BankStatementParsing"
    }
  },
  "LoggingSettings": {
    "EnablePerformanceLogging": true,
    "EnableDetailedErrorLogging": true,
    "LogProcessingMetrics": true,
    "MaxLogFileSizeMB": 100,
    "LogRetentionDays": 30
  }
}
```

## Custom Enrichers

### Processing Context Enricher
```csharp
public class ProcessingContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Add processing session ID if available
        if (LogContext.TryGetProperty("ProcessingSessionId", out var sessionId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessingSessionId", sessionId));
        }

        // Add file being processed if available
        if (LogContext.TryGetProperty("CurrentFile", out var fileName))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CurrentFile", fileName));
        }

        // Add account context if available
        if (LogContext.TryGetProperty("AccountId", out var accountId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AccountId", accountId));
        }
    }
}
```

### Performance Enricher
```csharp
public class PerformanceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Add memory usage
        var memoryUsage = GC.GetTotalMemory(false);
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryUsageMB", memoryUsage / 1024 / 1024));

        // Add timestamp with high precision
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("PreciseTimestamp", DateTimeOffset.UtcNow.ToString("O")));
    }
}
```

## Structured Logging Patterns

### File Processing Logging
```csharp
public class FileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;

    public async Task<ProcessingResult> ProcessFileAsync(string filePath, string accountId)
    {
        var processingSessionId = Guid.NewGuid().ToString();
        var fileName = Path.GetFileName(filePath);
        
        using (LogContext.PushProperty("ProcessingSessionId", processingSessionId))
        using (LogContext.PushProperty("CurrentFile", fileName))
        using (LogContext.PushProperty("AccountId", accountId))
        {
            _logger.LogInformation("Starting file processing for {FileName} in account {AccountId}", 
                fileName, accountId);

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // File validation
                _logger.LogDebug("Validating file {FileName}", fileName);
                await ValidateFileAsync(filePath);
                
                // PDF text extraction
                _logger.LogDebug("Extracting text from PDF {FileName}", fileName);
                var extractedText = await ExtractTextAsync(filePath);
                _logger.LogInformation("Extracted {TextLength} characters from {FileName}", 
                    extractedText.Length, fileName);

                // Transaction parsing
                _logger.LogDebug("Parsing transactions from {FileName}", fileName);
                var transactions = await ParseTransactionsAsync(extractedText, accountId);
                _logger.LogInformation("Parsed {TransactionCount} transactions from {FileName}", 
                    transactions.Count, fileName);

                // Database storage
                _logger.LogDebug("Storing {TransactionCount} transactions for {FileName}", 
                    transactions.Count, fileName);
                await StoreTransactionsAsync(transactions);

                stopwatch.Stop();
                _logger.LogInformation("Successfully processed {FileName} in {ElapsedMs}ms. " +
                    "Processed {TransactionCount} transactions",
                    fileName, stopwatch.ElapsedMilliseconds, transactions.Count);

                return ProcessingResult.Success(transactions.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to process {FileName} after {ElapsedMs}ms", 
                    fileName, stopwatch.ElapsedMilliseconds);
                return ProcessingResult.Failure(ex.Message);
            }
        }
    }
}
```

### Database Operation Logging
```csharp
public class TransactionRepository
{
    private readonly ILogger<TransactionRepository> _logger;
    private readonly BankStatementDbContext _context;

    public async Task<int> BulkInsertTransactionsAsync(IList<Transaction> transactions)
    {
        using (LogContext.PushProperty("OperationType", "BulkInsert"))
        {
            _logger.LogInformation("Starting bulk insert of {TransactionCount} transactions", 
                transactions.Count);

            var stopwatch = Stopwatch.StartNew();
            var insertedCount = 0;

            try
            {
                const int batchSize = 1000;
                var batches = transactions.Chunk(batchSize).ToList();
                
                _logger.LogDebug("Processing {BatchCount} batches of {BatchSize} transactions", 
                    batches.Count, batchSize);

                foreach (var (batch, index) in batches.Select((b, i) => (b, i)))
                {
                    await _context.Transactions.AddRangeAsync(batch);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                    
                    insertedCount += batch.Length;
                    
                    _logger.LogDebug("Completed batch {BatchNumber}/{TotalBatches}, " +
                        "inserted {BatchCount} transactions", 
                        index + 1, batches.Count, batch.Length);
                }

                stopwatch.Stop();
                _logger.LogInformation("Successfully inserted {InsertedCount} transactions in {ElapsedMs}ms " +
                    "({TransactionsPerSecond:F2} transactions/second)",
                    insertedCount, stopwatch.ElapsedMilliseconds, 
                    insertedCount / stopwatch.Elapsed.TotalSeconds);

                return insertedCount;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to insert transactions after {ElapsedMs}ms. " +
                    "Successfully inserted {InsertedCount}/{TotalCount} transactions",
                    stopwatch.ElapsedMilliseconds, insertedCount, transactions.Count);
                throw;
            }
        }
    }
}
```

### Error Logging with Context
```csharp
public class ErrorLoggingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            using (LogContext.PushProperty("RequestPath", context.Request.Path))
            using (LogContext.PushProperty("RequestMethod", context.Request.Method))
            using (LogContext.PushProperty("UserAgent", context.Request.Headers["User-Agent"].ToString()))
            using (LogContext.PushProperty("RemoteIpAddress", context.Connection.RemoteIpAddress?.ToString()))
            {
                _logger.LogError(ex, "Unhandled exception occurred processing request {RequestMethod} {RequestPath}",
                    context.Request.Method, context.Request.Path);
            }
            
            throw;
        }
    }
}
```

## Performance Logging

### Processing Metrics
```csharp
public class ProcessingMetricsLogger
{
    private readonly ILogger<ProcessingMetricsLogger> _logger;

    public void LogProcessingMetrics(ProcessingSession session)
    {
        _logger.LogInformation("Processing metrics for session {SessionId}: " +
            "Files processed: {FilesProcessed}, " +
            "Transactions extracted: {TransactionsExtracted}, " +
            "Total processing time: {TotalTimeMs}ms, " +
            "Average time per file: {AvgTimePerFileMs}ms, " +
            "Success rate: {SuccessRate:P2}",
            session.Id,
            session.FilesProcessed,
            session.TransactionsExtracted,
            session.TotalProcessingTime.TotalMilliseconds,
            session.AverageTimePerFile.TotalMilliseconds,
            session.SuccessRate);
    }

    public void LogPerformanceWarning(string operation, TimeSpan duration, TimeSpan threshold)
    {
        _logger.LogWarning("Performance warning: {Operation} took {ActualMs}ms, " +
            "which exceeds threshold of {ThresholdMs}ms",
            operation, duration.TotalMilliseconds, threshold.TotalMilliseconds);
    }
}
```

## Log Analysis and Monitoring

### Structured Query Examples
```bash
# Find all processing sessions for a specific account
jq 'select(.Properties.AccountId == "Account1")' Logs/Processing/processing-*.log

# Analyze processing performance
jq 'select(.MessageTemplate | contains("Successfully processed")) | 
    {file: .Properties.CurrentFile, duration: .Properties.ElapsedMs, transactions: .Properties.TransactionCount}' 
    Logs/Processing/processing-*.log

# Error analysis
jq 'select(.Level == "Error") | {timestamp: .Timestamp, message: .Message, exception: .Exception}' 
    Logs/Errors/errors-*.log
```

### Health Monitoring
```csharp
public class LoggingHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if log directories exist and are writable
            var logPaths = new[] { "Logs/Application", "Logs/Processing", "Logs/Errors" };
            
            foreach (var path in logPaths)
            {
                Directory.CreateDirectory(path);
                var testFile = Path.Combine(path, $"health-check-{Guid.NewGuid()}.tmp");
                await File.WriteAllTextAsync(testFile, "test", cancellationToken);
                File.Delete(testFile);
            }

            // Check log file sizes
            var data = logPaths.ToDictionary(
                path => path.Replace("/", "_"),
                path => GetDirectorySize(path));

            return HealthCheckResult.Healthy("Logging system is healthy", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Logging system check failed", ex);
        }
    }

    private long GetDirectorySize(string path)
    {
        return Directory.Exists(path) 
            ? Directory.GetFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            : 0;
    }
}
```

## Log Retention and Cleanup

### Automated Cleanup Service
```csharp
public class LogCleanupService : BackgroundService
{
    private readonly ILogger<LogCleanupService> _logger;
    private readonly IConfiguration _configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldLogsAsync();
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken); // Run daily
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log cleanup");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // Retry in 1 hour
            }
        }
    }

    private async Task CleanupOldLogsAsync()
    {
        var retentionDays = _configuration.GetValue<int>("LoggingSettings:LogRetentionDays", 30);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        var logDirectories = new[] { "Logs/Application", "Logs/Processing", "Logs/Errors" };
        
        foreach (var directory in logDirectories)
        {
            if (!Directory.Exists(directory)) continue;
            
            var oldFiles = Directory.GetFiles(directory, "*.log")
                .Where(f => File.GetCreationTime(f) < cutoffDate)
                .ToList();
                
            foreach (var file in oldFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted old log file: {FileName}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete log file: {FileName}", Path.GetFileName(file));
                }
            }
        }
    }
}
```

## Best Practices

### Logging Guidelines
1. **Use structured logging**: Always use template strings with named parameters
2. **Include context**: Add relevant properties using LogContext
3. **Performance-conscious**: Use Debug/Verbose levels for detailed information
4. **Meaningful messages**: Write clear, actionable log messages
5. **Avoid sensitive data**: Never log passwords, API keys, or PII
6. **Use correlation IDs**: Track operations across multiple components

### Example Template Usage
```csharp
// Good - Structured logging with context
_logger.LogInformation("User {UserId} processed {FileCount} files in {Duration}ms", 
    userId, fileCount, duration);

// Bad - String concatenation
_logger.LogInformation($"User {userId} processed {fileCount} files in {duration}ms");

// Good - Error with context
_logger.LogError(ex, "Failed to process file {FileName} for account {AccountId}", 
    fileName, accountId);

// Bad - Generic error message
_logger.LogError(ex, "File processing failed");
``` 