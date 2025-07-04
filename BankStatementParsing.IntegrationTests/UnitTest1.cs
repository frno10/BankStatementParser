using BankStatementParsing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BankStatementParsing.IntegrationTests;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly BankStatementParsingContext _context;
    private readonly ServiceProvider _serviceProvider;

    public DatabaseIntegrationTests()
    {
        var services = new ServiceCollection();
        
        services.AddDbContext<BankStatementParsingContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
            
        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<BankStatementParsingContext>();
        
        // Ensure the database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAccount_ShouldPersistToDatabase()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "1234567890",
            Name = "Test Account",
            IBAN = "DE89370400440532013000",
            BIC = "TESTBIC",
            Holder = "John Doe",
            Type = "Checking",
            Currency = "USD",
            Branch = "Main Branch"
        };

        // Act
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        // Assert
        var savedAccount = await _context.Accounts.FirstOrDefaultAsync();
        savedAccount.Should().NotBeNull();
        savedAccount!.AccountNumber.Should().Be("1234567890");
        savedAccount.Name.Should().Be("Test Account");
        savedAccount.IBAN.Should().Be("DE89370400440532013000");
        savedAccount.BIC.Should().Be("TESTBIC");
        savedAccount.Holder.Should().Be("John Doe");
        savedAccount.Type.Should().Be("Checking");
        savedAccount.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task AddStatement_ShouldPersistToDatabase()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "123456",
            Name = "Test Account",
            Currency = "USD"
        };

        var statement = new Statement
        {
            Account = account,
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            StatementNumber = "STMT001",
            OpeningBalance = 1000.0,
            ClosingBalance = 950.0,
            StatementName = "test-statement.pdf"
        };

        // Act
        _context.Accounts.Add(account);
        _context.Statements.Add(statement);
        await _context.SaveChangesAsync();

        // Assert
        var savedStatement = await _context.Statements
            .Include(s => s.Account)
            .FirstOrDefaultAsync();
            
        savedStatement.Should().NotBeNull();
        savedStatement!.StatementNumber.Should().Be("STMT001");
        savedStatement.OpeningBalance.Should().Be(1000.0);
        savedStatement.ClosingBalance.Should().Be(950.0);
        savedStatement.StatementName.Should().Be("test-statement.pdf");
        savedStatement.Account.Should().NotBeNull();
        savedStatement.Account.AccountNumber.Should().Be("123456");
    }



    [Fact]
    public async Task AddTransaction_ShouldPersistToDatabase()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "123456",
            Name = "Test Account",
            Currency = "USD"
        };

        var statement = new Statement
        {
            Account = account,
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            StatementNumber = "STMT001",
            StatementName = "test-statement.pdf"
        };

        var transaction = new Transaction
        {
            Statement = statement,
            Date = new DateTime(2024, 1, 15),
            Description = "Test Transaction",
            Amount = -50.25,
            Currency = "USD",
            Reference = "REF123"
        };

        // Act
        _context.Accounts.Add(account);
        _context.Statements.Add(statement);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Assert
        var savedTransaction = await _context.Transactions
            .Include(t => t.Statement)
            .ThenInclude(s => s.Account)
            .FirstOrDefaultAsync();
            
        savedTransaction.Should().NotBeNull();
        savedTransaction!.Description.Should().Be("Test Transaction");
        savedTransaction.Amount.Should().Be(-50.25);
        savedTransaction.Currency.Should().Be("USD");
        savedTransaction.Reference.Should().Be("REF123");
        savedTransaction.Statement.Should().NotBeNull();
        savedTransaction.Statement.StatementNumber.Should().Be("STMT001");
        savedTransaction.Statement.Account.AccountNumber.Should().Be("123456");
    }

    [Fact]
    public async Task StatementWithTransactions_ShouldMaintainRelationships()
    {
        // Arrange
        var account = new Account
        {
            AccountNumber = "123456",
            Name = "Test Account",
            Currency = "USD"
        };

        var statement = new Statement
        {
            Account = account,
            PeriodStart = new DateTime(2024, 1, 1),
            PeriodEnd = new DateTime(2024, 1, 31),
            StatementNumber = "STMT001"
        };

        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Statement = statement,
                Date = new DateTime(2024, 1, 10),
                Description = "Transaction 1",
                Amount = -100.0,
                Currency = "USD"
            },
            new Transaction
            {
                Statement = statement,
                Date = new DateTime(2024, 1, 15),
                Description = "Transaction 2",
                Amount = 200.0,
                Currency = "USD"
            }
        };

        statement.Transactions = transactions;

        // Act
        _context.Accounts.Add(account);
        _context.Statements.Add(statement);
        await _context.SaveChangesAsync();

        // Assert
        var savedStatement = await _context.Statements
            .Include(s => s.Transactions)
            .Include(s => s.Account)
            .FirstOrDefaultAsync();

        savedStatement.Should().NotBeNull();
        savedStatement!.Transactions.Should().HaveCount(2);
        
        var firstTransaction = savedStatement.Transactions.FirstOrDefault(t => t.Description == "Transaction 1");
        firstTransaction.Should().NotBeNull();
        firstTransaction!.Amount.Should().Be(-100.0);
        
        var secondTransaction = savedStatement.Transactions.FirstOrDefault(t => t.Description == "Transaction 2");
        secondTransaction.Should().NotBeNull();
        secondTransaction!.Amount.Should().Be(200.0);
    }

    [Fact]
    public async Task QueryStatementsByAccount_ShouldReturnCorrectResults()
    {
        // Arrange
        var account1 = new Account { AccountNumber = "111", Name = "Account 1", Currency = "USD" };
        var account2 = new Account { AccountNumber = "222", Name = "Account 2", Currency = "EUR" };

        var statements1 = new List<Statement>
        {
            new Statement { Account = account1, StatementNumber = "STMT001", StatementName = "stmt1.pdf" },
            new Statement { Account = account1, StatementNumber = "STMT002", StatementName = "stmt2.pdf" }
        };

        var statements2 = new List<Statement>
        {
            new Statement { Account = account2, StatementNumber = "STMT003", StatementName = "stmt3.pdf" }
        };

        _context.Accounts.AddRange(account1, account2);
        _context.Statements.AddRange(statements1);
        _context.Statements.AddRange(statements2);
        await _context.SaveChangesAsync();

        // Act
        var account1Results = await _context.Statements
            .Include(s => s.Account)
            .Where(s => s.Account.AccountNumber == "111")
            .ToListAsync();

        var account2Results = await _context.Statements
            .Include(s => s.Account)
            .Where(s => s.Account.AccountNumber == "222")
            .ToListAsync();

        // Assert
        account1Results.Should().HaveCount(2);
        account1Results.All(s => s.Account.AccountNumber == "111").Should().BeTrue();
        account1Results.Should().Contain(s => s.StatementNumber == "STMT001");
        account1Results.Should().Contain(s => s.StatementNumber == "STMT002");

        account2Results.Should().HaveCount(1);
        account2Results[0].Account.AccountNumber.Should().Be("222");
        account2Results[0].StatementNumber.Should().Be("STMT003");
    }

    [Fact]
    public async Task QueryTransactionsByDateRange_ShouldReturnCorrectResults()
    {
        // Arrange
        var account = new Account { AccountNumber = "123456", Name = "Test Account", Currency = "USD" };
        var statement = new Statement { Account = account, StatementNumber = "STMT001" };

        var transactions = new List<Transaction>
        {
            new Transaction { Statement = statement, Date = new DateTime(2024, 1, 5), Description = "Early", Amount = 100.0 },
            new Transaction { Statement = statement, Date = new DateTime(2024, 1, 15), Description = "Middle", Amount = 200.0 },
            new Transaction { Statement = statement, Date = new DateTime(2024, 1, 25), Description = "Late", Amount = 300.0 }
        };

        _context.Accounts.Add(account);
        _context.Statements.Add(statement);
        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        // Act
        var startDate = new DateTime(2024, 1, 10);
        var endDate = new DateTime(2024, 1, 20);
        
        var filteredTransactions = await _context.Transactions
            .Where(t => t.Date >= startDate && t.Date <= endDate)
            .ToListAsync();

        // Assert
        filteredTransactions.Should().HaveCount(1);
        filteredTransactions[0].Description.Should().Be("Middle");
        filteredTransactions[0].Amount.Should().Be(200.0);
    }

    [Fact]
    public async Task UpdateStatementBalances_ShouldPersistChanges()
    {
        // Arrange
        var account = new Account { AccountNumber = "123456", Name = "Test Account" };
        var statement = new Statement
        {
            Account = account,
            StatementNumber = "STMT001",
            OpeningBalance = 1000.0,
            ClosingBalance = 1000.0
        };

        _context.Accounts.Add(account);
        _context.Statements.Add(statement);
        await _context.SaveChangesAsync();

        // Act
        statement.ClosingBalance = 950.0;
        statement.TotalDebits = 50.0;
        await _context.SaveChangesAsync();

        // Assert
        var updatedStatement = await _context.Statements.FirstOrDefaultAsync();
        updatedStatement.Should().NotBeNull();
        updatedStatement!.ClosingBalance.Should().Be(950.0);
        updatedStatement.TotalDebits.Should().Be(50.0);
    }

    [Fact]
    public async Task DeleteStatement_ShouldRemoveFromDatabase()
    {
        // Arrange
        var account = new Account { AccountNumber = "123456", Name = "Test Account" };
        var statement = new Statement { Account = account, StatementNumber = "STMT001" };

        _context.Accounts.Add(account);
        _context.Statements.Add(statement);
        await _context.SaveChangesAsync();

        var statementId = statement.Id;

        // Act
        _context.Statements.Remove(statement);
        await _context.SaveChangesAsync();

        // Assert
        var deletedStatement = await _context.Statements.FindAsync(statementId);
        deletedStatement.Should().BeNull();
    }

    [Fact]
    public async Task AddMerchantWithTags_ShouldMaintainRelationships()
    {
        // Arrange
        var merchant = new Merchant
        {
            Name = "Test Store",
            Address = "123 Main St",
            MerchantIdentifier = "TST001"
        };

        var tag = new Tag { Name = "Retail" };
        var merchantTag = new MerchantTag { Merchant = merchant, Tag = tag };

        merchant.MerchantTags.Add(merchantTag);
        tag.MerchantTags.Add(merchantTag);

        // Act
        _context.Merchants.Add(merchant);
        _context.Tags.Add(tag);
        _context.MerchantTags.Add(merchantTag);
        await _context.SaveChangesAsync();

        // Assert
        var savedMerchant = await _context.Merchants
            .Include(m => m.MerchantTags)
            .ThenInclude(mt => mt.Tag)
            .FirstOrDefaultAsync();

        savedMerchant.Should().NotBeNull();
        savedMerchant!.Name.Should().Be("Test Store");
        savedMerchant.MerchantTags.Should().HaveCount(1);
        savedMerchant.MerchantTags.First().Tag.Name.Should().Be("Retail");
    }

    public void Dispose()
    {
        _context?.Dispose();
        _serviceProvider?.Dispose();
    }
}
