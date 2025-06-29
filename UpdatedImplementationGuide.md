# Updated Bank Statement Parsing Implementation Guide

## Overview
This guide provides the updated implementation approach based on specific requirements:
- **SQLite Database** instead of SQL Server
- **Mapster** instead of AutoMapper for better performance
- **Extensive Serilog Logging** with console + file outputs
- **File-based Processing** with multi-account folder structure
- **Drop & Process** workflow for easy file handling

## Project Structure

```
BankStatementParsing/
├── AccountData/                    # File processing folders
│   ├── Account1/
│   │   ├── Inbox/                 # Drop PDF files here
│   │   ├── Processing/            # Files being processed
│   │   ├── Processed/             # Successfully processed
│   │   └── Failed/                # Failed with error logs
│   ├── Account2/
│   └── Account3/
├── Database/                       # SQLite database location
├── Logs/                          # Serilog output folders
│   ├── Application/               # General app logs
│   ├── Processing/                # File processing logs
│   └── Errors/                    # Error-specific logs
├── BankStatementParsing.Api/       # Web API project
├── BankStatementParsing.Core/      # Domain models & interfaces
├── BankStatementParsing.Infrastructure/ # Data access & file ops
├── BankStatementParsing.Services/  # Business logic
└── BankStatementParsing.Tests/     # Unit & integration tests
```

## Step 1: Create Solution and Projects

### Initialize Projects
```bash
# Create solution
dotnet new sln -n BankStatementParsing

# Create projects
dotnet new webapi -n BankStatementParsing.Api
dotnet new classlib -n BankStatementParsing.Core
dotnet new classlib -n BankStatementParsing.Infrastructure
dotnet new classlib -n BankStatementParsing.Services
dotnet new xunit -n BankStatementParsing.Tests

# Add to solution
dotnet sln add **/*.csproj
```

### Add Project References
```bash
# API references
cd BankStatementParsing.Api
dotnet add reference ../BankStatementParsing.Core
dotnet add reference ../BankStatementParsing.Infrastructure
dotnet add reference ../BankStatementParsing.Services

# Services references
cd ../BankStatementParsing.Services
dotnet add reference ../BankStatementParsing.Core

# Infrastructure references
cd ../BankStatementParsing.Infrastructure
dotnet add reference ../BankStatementParsing.Core

# Tests references
cd ../BankStatementParsing.Tests
dotnet add reference ../BankStatementParsing.Api
dotnet add reference ../BankStatementParsing.Core
dotnet add reference ../BankStatementParsing.Services
dotnet add reference ../BankStatementParsing.Infrastructure
```

## Step 2: Install NuGet Packages

### API Project
```bash
cd BankStatementParsing.Api
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Formatting.Compact
dotnet add package Swashbuckle.AspNetCore
dotnet add package Mapster
dotnet add package Mapster.DependencyInjection
```

### Core Project
```bash
cd ../BankStatementParsing.Core
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package System.ComponentModel.Annotations
```

### Infrastructure Project
```bash
cd ../BankStatementParsing.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.Extensions.Configuration
dotnet add package PdfPig
dotnet add package Microsoft.Extensions.Hosting
```

### Services Project
```bash
cd ../BankStatementParsing.Services
dotnet add package Microsoft.Extensions.Logging
dotnet add package FluentValidation
dotnet add package Mapster
```

### Tests Project
```bash
cd ../BankStatementParsing.Tests
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Moq
dotnet add package FluentAssertions
```

## Step 3: Configuration Files

### appsettings.json (Updated for SQLite + Serilog)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Database/BankStatements.db;Cache=Shared;"
  },
  "AccountProcessing": {
    "BasePath": "AccountData",
    "Accounts": {
      "Account1": {
        "Name": "Personal Checking",
        "BankType": "Chase",
        "AutoDetectBank": true,
        "ProcessingRules": {
          "RequireBalanceValidation": true,
          "AllowDuplicates": false
        }
      },
      "Account2": {
        "Name": "Business Account", 
        "BankType": "BankOfAmerica",
        "AutoDetectBank": false
      },
      "Account3": {
        "Name": "Savings Account",
        "BankType": "Auto",
        "AutoDetectBank": true
      }
    },
    "FileWatcher": {
      "EnableFileWatcher": true,
      "ProcessingIntervalSeconds": 30,
      "MaxConcurrentFiles": 3,
      "MaxRetryAttempts": 3,
      "RetryDelaySeconds": 60
    }
  },
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
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/Errors/errors-.log",
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
          "restrictedToMinimumLevel": "Error",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 90
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

## Step 4: Core Entities (Updated for SQLite)

### Account Entity
```csharp
// BankStatementParsing.Core/Entities/Account.cs
using System.ComponentModel.DataAnnotations;

namespace BankStatementParsing.Core.Entities;

public class Account
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? AccountNumber { get; set; }
    
    [MaxLength(100)]
    public string? BankName { get; set; }
    
    [MaxLength(50)]
    public string AccountType { get; set; } = "Checking";
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<BankStatement> BankStatements { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
}
```

### BankStatement Entity
```csharp
// BankStatementParsing.Core/Entities/BankStatement.cs
using System.ComponentModel.DataAnnotations;
using BankStatementParsing.Core.Enums;

namespace BankStatementParsing.Core.Entities;

public class BankStatement
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? BankName { get; set; }
    
    public DateTime? StatementPeriodStart { get; set; }
    public DateTime? StatementPeriodEnd { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Account Account { get; set; } = null!;
    public List<Transaction> Transactions { get; set; } = new();
    public List<ProcessingLog> ProcessingLogs { get; set; } = new();
}
```

### Transaction Entity
```csharp
// BankStatementParsing.Core/Entities/Transaction.cs
using System.ComponentModel.DataAnnotations;
using BankStatementParsing.Core.Enums;

namespace BankStatementParsing.Core.Entities;

public class Transaction
{
    public int Id { get; set; }
    public int BankStatementId { get; set; }
    public int AccountId { get; set; }
    
    public DateTime TransactionDate { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    public TransactionType TransactionType { get; set; }
    
    [MaxLength(100)]
    public string? Category { get; set; }
    
    [MaxLength(100)]
    public string? SubCategory { get; set; }
    
    [MaxLength(100)]
    public string? Reference { get; set; }
    
    public decimal? Balance { get; set; }
    public bool IsReconciled { get; set; } = false;
    
    [MaxLength(500)]
    public string? Tags { get; set; } // JSON array
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public BankStatement BankStatement { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
```

### ProcessingLog Entity
```csharp
// BankStatementParsing.Core/Entities/ProcessingLog.cs
using System.ComponentModel.DataAnnotations;

namespace BankStatementParsing.Core.Entities;

public class ProcessingLog
{
    public int Id { get; set; }
    public int? BankStatementId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string LogLevel { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public string? Exception { get; set; }
    
    [MaxLength(100)]
    public string? ProcessingStep { get; set; }
    
    public int? ExecutionTime { get; set; } // Milliseconds
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public BankStatement? BankStatement { get; set; }
}
```

### Enums
```csharp
// BankStatementParsing.Core/Enums/ProcessingStatus.cs
namespace BankStatementParsing.Core.Enums;

public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

// BankStatementParsing.Core/Enums/TransactionType.cs
namespace BankStatementParsing.Core.Enums;

public enum TransactionType
{
    Debit,
    Credit
}
```

## Step 5: DbContext for SQLite

```csharp
// BankStatementParsing.Infrastructure/Data/BankStatementDbContext.cs
using BankStatementParsing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankStatementParsing.Infrastructure.Data;

public class BankStatementDbContext : DbContext
{
    public BankStatementDbContext(DbContextOptions<BankStatementDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<BankStatement> BankStatements { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<ProcessingLog> ProcessingLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AccountNumber).HasMaxLength(50);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
        });

        // BankStatement configuration
        modelBuilder.Entity<BankStatement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ClosingBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasConversion<string>();
            
            entity.HasOne(e => e.Account)
                  .WithMany(a => a.BankStatements)
                  .HasForeignKey(e => e.AccountId);
        });

        // Transaction configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TransactionType).HasConversion<string>();
            
            entity.HasOne(e => e.BankStatement)
                  .WithMany(bs => bs.Transactions)
                  .HasForeignKey(e => e.BankStatementId);
                  
            entity.HasOne(e => e.Account)
                  .WithMany(a => a.Transactions)
                  .HasForeignKey(e => e.AccountId);

            // Composite index for duplicate detection
            entity.HasIndex(e => new { e.AccountId, e.TransactionDate, e.Amount, e.Description })
                  .HasDatabaseName("IX_Transactions_Duplicate_Detection");
        });

        // ProcessingLog configuration
        modelBuilder.Entity<ProcessingLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LogLevel).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Message).IsRequired();
            
            entity.HasOne(e => e.BankStatement)
                  .WithMany(bs => bs.ProcessingLogs)
                  .HasForeignKey(e => e.BankStatementId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
```

## Step 6: Mapster Configuration

```csharp
// BankStatementParsing.Core/Mapping/MappingConfig.cs
using BankStatementParsing.Core.DTOs;
using BankStatementParsing.Core.Entities;
using Mapster;

namespace BankStatementParsing.Core.Mapping;

public static class MappingConfig
{
    public static void Configure()
    {
        // Account mappings
        TypeAdapterConfig<Account, AccountDto>.NewConfig()
            .Map(dest => dest.TotalTransactions, src => src.Transactions.Count)
            .Map(dest => dest.LastStatementDate, src => src.BankStatements
                .OrderByDescending(bs => bs.CreatedAt)
                .FirstOrDefault() != null ? 
                src.BankStatements.OrderByDescending(bs => bs.CreatedAt).First().CreatedAt : (DateTime?)null);

        // BankStatement mappings
        TypeAdapterConfig<BankStatement, BankStatementDto>.NewConfig()
            .Map(dest => dest.TransactionCount, src => src.Transactions.Count)
            .Map(dest => dest.Transactions, src => src.Transactions.Adapt<List<TransactionDto>>());

        // Transaction mappings
        TypeAdapterConfig<Transaction, TransactionDto>.NewConfig();

        // Reverse mappings for creating entities
        TypeAdapterConfig<CreateAccountDto, Account>.NewConfig()
            .Map(dest => dest.CreatedAt, src => DateTime.UtcNow)
            .Map(dest => dest.UpdatedAt, src => DateTime.UtcNow)
            .Map(dest => dest.IsActive, src => true);
    }
}
```

## Step 7: File Processing Service with Extensive Logging

```csharp
// BankStatementParsing.Services/FileProcessingService.cs
using BankStatementParsing.Core.Entities;
using BankStatementParsing.Core.Enums;
using BankStatementParsing.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace BankStatementParsing.Services;

public class FileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;
    private readonly BankStatementDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPdfParsingService _pdfParser;

    public FileProcessingService(
        ILogger<FileProcessingService> logger,
        BankStatementDbContext context,
        IConfiguration configuration,
        IPdfParsingService pdfParser)
    {
        _logger = logger;
        _context = context;
        _configuration = configuration;
        _pdfParser = pdfParser;
    }

    public async Task<ProcessingResult> ProcessFileAsync(string filePath, string accountName)
    {
        var processingSessionId = Guid.NewGuid().ToString();
        var fileName = Path.GetFileName(filePath);
        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("ProcessingSessionId", processingSessionId))
        using (LogContext.PushProperty("CurrentFile", fileName))
        using (LogContext.PushProperty("AccountName", accountName))
        {
            _logger.LogInformation("Starting file processing for {FileName} in account {AccountName}",
                fileName, accountName);

            try
            {
                // Step 1: Move file to Processing folder
                var processingPath = await MoveToProcessingAsync(filePath, accountName);
                
                // Step 2: Get or create account
                var account = await GetOrCreateAccountAsync(accountName);
                
                // Step 3: Create bank statement record
                var bankStatement = await CreateBankStatementAsync(processingPath, account.Id);
                
                // Step 4: Extract and parse PDF
                _logger.LogDebug("Extracting text from PDF {FileName}", fileName);
                var extractedData = await _pdfParser.ExtractDataAsync(processingPath);
                
                _logger.LogInformation("Extracted {TransactionCount} transactions from {FileName}",
                    extractedData.Transactions.Count, fileName);

                // Step 5: Store transactions
                await StoreTransactionsAsync(extractedData.Transactions, bankStatement.Id, account.Id);
                
                // Step 6: Update statement status
                await UpdateStatementStatusAsync(bankStatement.Id, ProcessingStatus.Completed);
                
                // Step 7: Move to Processed folder
                await MoveToProcessedAsync(processingPath, accountName);
                
                stopwatch.Stop();
                _logger.LogInformation(
                    "Successfully processed {FileName} in {ElapsedMs}ms. " +
                    "Extracted {TransactionCount} transactions",
                    fileName, stopwatch.ElapsedMilliseconds, extractedData.Transactions.Count);

                return new ProcessingResult
                {
                    Success = true,
                    TransactionCount = extractedData.Transactions.Count,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to process {FileName} after {ElapsedMs}ms",
                    fileName, stopwatch.ElapsedMilliseconds);

                // Move to Failed folder with error info
                await MoveToFailedAsync(filePath, accountName, ex.Message);

                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }
    }

    private async Task<string> MoveToProcessingAsync(string filePath, string accountName)
    {
        var fileName = Path.GetFileName(filePath);
        var processingPath = Path.Combine("AccountData", accountName, "Processing", fileName);
        
        Directory.CreateDirectory(Path.GetDirectoryName(processingPath)!);
        File.Move(filePath, processingPath);
        
        _logger.LogDebug("Moved {FileName} to processing folder", fileName);
        return processingPath;
    }

    // Additional methods would follow the same logging pattern...
}
```

## Step 8: Startup Configuration

```csharp
// BankStatementParsing.Api/Program.cs
using BankStatementParsing.Infrastructure.Data;
using BankStatementParsing.Core.Mapping;
using BankStatementParsing.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Mapster;
using MapsterMapper;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
    .WriteTo.File(
        path: "Logs/Application/app-.log",
        formatter: new Serilog.Formatting.Compact.CompactJsonFormatter(),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .WriteTo.File(
        path: "Logs/Errors/errors-.log",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        formatter: new Serilog.Formatting.Compact.CompactJsonFormatter(),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<BankStatementDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Mapster
MappingConfig.Configure();
builder.Services.AddSingleton(TypeAdapterConfig.GlobalSettings);
builder.Services.AddScoped<IMapper, ServiceMapper>();

// Services
builder.Services.AddScoped<FileProcessingService>();
builder.Services.AddHostedService<FileWatcherService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BankStatementDbContext>();
    await context.Database.EnsureCreatedAsync();
    
    // Optimize SQLite
    await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
    await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=-10000;");
}

app.Run();
```

## Next Steps

1. **Run the setup commands** to create the solution structure
2. **Install the packages** as specified above
3. **Create the entities and DTOs** in the Core project
4. **Implement the DbContext** in Infrastructure
5. **Configure Mapster** for object mapping
6. **Implement file processing services** with extensive logging
7. **Create the file watcher service** for automatic processing
8. **Test the application** by dropping PDF files into account folders

This updated implementation provides:
- ✅ **SQLite database** with optimized configuration
- ✅ **Mapster** for high-performance object mapping
- ✅ **Extensive Serilog logging** with multiple outputs
- ✅ **File-based processing** with automatic folder management
- ✅ **Multi-account support** with isolated processing
- ✅ **Background file watching** for automated processing

The application will monitor the `AccountData/*/Inbox/` folders and automatically process any PDF files dropped there, with comprehensive logging of every step. 