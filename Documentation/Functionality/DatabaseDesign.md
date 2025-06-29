# Database Design - SQLite Implementation

## Overview
The application uses SQLite as the primary database for storing parsed bank statement data, providing a lightweight, file-based solution with excellent performance.

## Database Schema

### Core Tables

#### Accounts Table
```sql
CREATE TABLE Accounts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name NVARCHAR(100) NOT NULL,
    AccountNumber NVARCHAR(50),
    BankName NVARCHAR(100),
    AccountType NVARCHAR(50), -- Checking, Savings, Credit
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

#### BankStatements Table
```sql
CREATE TABLE BankStatements (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountId INTEGER NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FilePath NVARCHAR(500) NOT NULL,
    BankName NVARCHAR(100),
    StatementPeriodStart DATE,
    StatementPeriodEnd DATE,
    OpeningBalance DECIMAL(18,2),
    ClosingBalance DECIMAL(18,2),
    ProcessedAt DATETIME,
    Status NVARCHAR(20) DEFAULT 'Pending', -- Pending, Processing, Completed, Failed
    ErrorMessage NTEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (AccountId) REFERENCES Accounts(Id)
);
```

#### Transactions Table
```sql
CREATE TABLE Transactions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BankStatementId INTEGER NOT NULL,
    AccountId INTEGER NOT NULL,
    TransactionDate DATE NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    TransactionType NVARCHAR(10) NOT NULL, -- Debit, Credit
    Category NVARCHAR(100),
    SubCategory NVARCHAR(100),
    Reference NVARCHAR(100),
    Balance DECIMAL(18,2),
    IsReconciled BOOLEAN DEFAULT 0,
    Tags NVARCHAR(500), -- JSON array of tags
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (BankStatementId) REFERENCES BankStatements(Id),
    FOREIGN KEY (AccountId) REFERENCES Accounts(Id)
);
```

#### ProcessingLogs Table
```sql
CREATE TABLE ProcessingLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BankStatementId INTEGER,
    LogLevel NVARCHAR(20) NOT NULL, -- Debug, Info, Warning, Error
    Message NTEXT NOT NULL,
    Exception NTEXT,
    ProcessingStep NVARCHAR(100),
    ExecutionTime INTEGER, -- Milliseconds
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (BankStatementId) REFERENCES BankStatements(Id)
);
```

### Indexes for Performance

```sql
-- Transaction queries by account and date
CREATE INDEX IX_Transactions_AccountId_Date 
ON Transactions(AccountId, TransactionDate);

-- Transaction queries by amount (for duplicate detection)
CREATE INDEX IX_Transactions_Amount_Date 
ON Transactions(Amount, TransactionDate, AccountId);

-- Statement queries by account
CREATE INDEX IX_BankStatements_AccountId 
ON BankStatements(AccountId);

-- Processing status queries
CREATE INDEX IX_BankStatements_Status 
ON BankStatements(Status, CreatedAt);

-- Log queries by statement
CREATE INDEX IX_ProcessingLogs_StatementId 
ON ProcessingLogs(BankStatementId, CreatedAt);

-- Full-text search on transaction descriptions
CREATE VIRTUAL TABLE Transactions_FTS USING fts5(
    Description, 
    Category,
    content='Transactions',
    content_rowid='Id'
);
```

## Entity Framework Configuration

### DbContext Configuration
```csharp
public class BankStatementDbContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<BankStatement> BankStatements { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<ProcessingLog> ProcessingLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = $"Data Source={GetDatabasePath()};Cache=Shared;";
        optionsBuilder.UseSqlite(connectionString);
        
        // Enable Write-Ahead Logging for better concurrency
        optionsBuilder.UseSqlite(connectionString, options =>
        {
            options.CommandTimeout(30);
        });
    }

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
            entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(10);
            
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
    }

    private string GetDatabasePath()
    {
        var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "BankStatements.db");
    }
}
```

## Performance Optimizations

### Connection Configuration
```csharp
public static class SQLiteConfiguration
{
    public static void ConfigureDatabase(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlite(connectionString, sqliteOptions =>
        {
            sqliteOptions.CommandTimeout(60);
        });

        // Enable optimizations
        options.EnableSensitiveDataLogging(false);
        options.EnableServiceProviderCaching();
        options.EnableDetailedErrors(false);
    }

    public static async Task OptimizeDatabaseAsync(BankStatementDbContext context)
    {
        // Enable WAL mode for better concurrency
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        
        // Optimize for faster writes
        await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
        
        // Increase cache size (10MB)
        await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=-10000;");
        
        // Enable memory-mapped I/O
        await context.Database.ExecuteSqlRawAsync("PRAGMA mmap_size=268435456;");
        
        // Analyze tables for query optimization
        await context.Database.ExecuteSqlRawAsync("ANALYZE;");
    }
}
```

### Bulk Insert Operations
```csharp
public static class BulkInsertExtensions
{
    public static async Task BulkInsertTransactionsAsync(
        this BankStatementDbContext context,
        IEnumerable<Transaction> transactions)
    {
        const int batchSize = 1000;
        var batches = transactions.Chunk(batchSize);

        foreach (var batch in batches)
        {
            await context.Transactions.AddRangeAsync(batch);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear(); // Free memory
        }
    }
}
```

## Data Access Patterns

### Repository Implementation
```csharp
public class TransactionRepository : ITransactionRepository
{
    private readonly BankStatementDbContext _context;

    public async Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(
        int accountId, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        var query = _context.Transactions
            .Where(t => t.AccountId == accountId);

        if (startDate.HasValue)
            query = query.Where(t => t.TransactionDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.TransactionDate <= endDate.Value);

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> IsDuplicateTransactionAsync(Transaction transaction)
    {
        return await _context.Transactions
            .AnyAsync(t => 
                t.AccountId == transaction.AccountId &&
                t.TransactionDate == transaction.TransactionDate &&
                t.Amount == transaction.Amount &&
                t.Description == transaction.Description);
    }
}
```

## Backup and Maintenance

### Automated Backup
```csharp
public class DatabaseMaintenanceService
{
    public async Task CreateBackupAsync()
    {
        var backupPath = Path.Combine("Database", "Backups", 
            $"BankStatements_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        
        using var sourceConnection = new SqliteConnection(GetConnectionString());
        using var backupConnection = new SqliteConnection($"Data Source={backupPath}");
        
        await sourceConnection.OpenAsync();
        await backupConnection.OpenAsync();
        
        sourceConnection.BackupDatabase(backupConnection);
    }

    public async Task OptimizeDatabaseAsync()
    {
        using var context = new BankStatementDbContext();
        
        // Vacuum to reclaim space
        await context.Database.ExecuteSqlRawAsync("VACUUM;");
        
        // Update statistics
        await context.Database.ExecuteSqlRawAsync("ANALYZE;");
        
        // Rebuild indexes if needed
        await context.Database.ExecuteSqlRawAsync("REINDEX;");
    }
}
```

## Migration Strategy

### Initial Migration
```csharp
// Add migration: dotnet ef migrations add InitialCreate
// Update database: dotnet ef database update

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create tables with optimized schema
        // Add indexes for performance
        // Configure foreign keys and constraints
    }
}
```

## Monitoring and Health Checks

### Database Health Check
```csharp
public class SqliteHealthCheck : IHealthCheck
{
    private readonly BankStatementDbContext _context;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1;", cancellationToken);
            
            var dbSize = new FileInfo(_context.GetDatabasePath()).Length;
            var data = new Dictionary<string, object>
            {
                ["database_size_mb"] = dbSize / 1024 / 1024,
                ["connection_state"] = _context.Database.GetDbConnection().State.ToString()
            };

            return HealthCheckResult.Healthy("SQLite database is responsive", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQLite database check failed", ex);
        }
    }
}
``` 