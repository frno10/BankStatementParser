using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BankStatementParsing.Infrastructure;

public class BankStatementParsingContext : DbContext
{
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Statement> Statements { get; set; }
    public DbSet<Merchant> Merchants { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<MerchantTag> MerchantTags { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionTag> TransactionTags { get; set; }

    public BankStatementParsingContext(DbContextOptions<BankStatementParsingContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite keys for many-to-many tables
        modelBuilder.Entity<MerchantTag>().HasKey(mt => new { mt.MerchantId, mt.TagId });
        modelBuilder.Entity<TransactionTag>().HasKey(tt => new { tt.TransactionId, tt.TagId });

        // Relationships
        modelBuilder.Entity<Statement>()
            .HasOne(s => s.Account)
            .WithMany(a => a.Statements)
            .HasForeignKey(s => s.AccountId);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Statement)
            .WithMany(s => s.Transactions)
            .HasForeignKey(t => t.StatementId);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Merchant)
            .WithMany(m => m.Transactions)
            .HasForeignKey(t => t.MerchantId);

        modelBuilder.Entity<MerchantTag>()
            .HasOne(mt => mt.Merchant)
            .WithMany(m => m.MerchantTags)
            .HasForeignKey(mt => mt.MerchantId);

        modelBuilder.Entity<MerchantTag>()
            .HasOne(mt => mt.Tag)
            .WithMany(t => t.MerchantTags)
            .HasForeignKey(mt => mt.TagId);

        modelBuilder.Entity<TransactionTag>()
            .HasOne(tt => tt.Transaction)
            .WithMany(t => t.TransactionTags)
            .HasForeignKey(tt => tt.TransactionId);

        modelBuilder.Entity<TransactionTag>()
            .HasOne(tt => tt.Tag)
            .WithMany(t => t.TransactionTags)
            .HasForeignKey(tt => tt.TagId);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = System.IO.Path.Combine("..", "Database", "bankstatements.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}

public class Account
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? BankCode { get; set; }
    public string? Name { get; set; }
    public string? IBAN { get; set; }
    public string? BIC { get; set; }
    public string? Holder { get; set; }
    public string? Address { get; set; }
    public string? Type { get; set; }
    public string? Currency { get; set; }
    public string? Branch { get; set; }
    public string? Contact { get; set; }
    public ICollection<Statement> Statements { get; set; } = new List<Statement>();
}

public class Statement
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string? StatementNumber { get; set; }
    public double? OpeningBalance { get; set; }
    public double? ClosingBalance { get; set; }
    public double? TotalDebits { get; set; }
    public double? TotalCredits { get; set; }
    public int? NumDebits { get; set; }
    public int? NumCredits { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

public class Merchant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? MerchantIdentifier { get; set; }
    public string? Notes { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<MerchantTag> MerchantTags { get; set; } = new List<MerchantTag>();
}

public class Tag
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public ICollection<MerchantTag> MerchantTags { get; set; } = new List<MerchantTag>();
    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}

public class MerchantTag
{
    public int MerchantId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class Transaction
{
    public int Id { get; set; }
    public int StatementId { get; set; }
    public Statement Statement { get; set; } = null!;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public double Amount { get; set; }
    public string? Currency { get; set; }
    public string? Reference { get; set; }
    public int? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public double? Countervalue { get; set; }
    public string? OriginalCurrency { get; set; }
    public double? ExchangeRate { get; set; }
    public string? ExtraInfo { get; set; }
    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}

public class TransactionTag
{
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
} 