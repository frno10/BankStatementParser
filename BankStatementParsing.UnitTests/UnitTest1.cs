using BankStatementParsing.Core.Entities;
using BankStatementParsing.Core.Enums;
using FluentAssertions;

namespace BankStatementParsing.UnitTests;

public class BankStatementEntityTests
{
    [Fact]
    public void BankStatement_ShouldCreateInstanceWithDefaultValues()
    {
        // Act
        var bankStatement = new BankStatement();

        // Assert
        bankStatement.Id.Should().Be(0);
        bankStatement.UserId.Should().Be(0);
        bankStatement.FileName.Should().Be(string.Empty);
        bankStatement.BankName.Should().Be(string.Empty);
        bankStatement.AccountNumber.Should().Be(string.Empty);
        bankStatement.OpeningBalance.Should().Be(0);
        bankStatement.ClosingBalance.Should().Be(0);
        bankStatement.Status.Should().Be(ProcessingStatus.Uploaded);
        bankStatement.FilePath.Should().Be(string.Empty);
        bankStatement.Currency.Should().Be("RUB");
        bankStatement.Transactions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void BankStatement_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var uploadedAt = DateTime.Now;
        var processedAt = DateTime.Now.AddMinutes(5);

        // Act
        var bankStatement = new BankStatement
        {
            Id = 1,
            UserId = 123,
            FileName = "statement.pdf",
            BankName = "TestBank",
            AccountNumber = "1234567890",
            StatementPeriodStart = startDate,
            StatementPeriodEnd = endDate,
            OpeningBalance = 1000.50m,
            ClosingBalance = 950.75m,
            UploadedAt = uploadedAt,
            ProcessedAt = processedAt,
            Status = ProcessingStatus.Completed,
            FilePath = "/path/to/statement.pdf",
            AccountHolderName = "John Doe",
            Currency = "USD"
        };

        // Assert
        bankStatement.Id.Should().Be(1);
        bankStatement.UserId.Should().Be(123);
        bankStatement.FileName.Should().Be("statement.pdf");
        bankStatement.BankName.Should().Be("TestBank");
        bankStatement.AccountNumber.Should().Be("1234567890");
        bankStatement.StatementPeriodStart.Should().Be(startDate);
        bankStatement.StatementPeriodEnd.Should().Be(endDate);
        bankStatement.OpeningBalance.Should().Be(1000.50m);
        bankStatement.ClosingBalance.Should().Be(950.75m);
        bankStatement.UploadedAt.Should().Be(uploadedAt);
        bankStatement.ProcessedAt.Should().Be(processedAt);
        bankStatement.Status.Should().Be(ProcessingStatus.Completed);
        bankStatement.FilePath.Should().Be("/path/to/statement.pdf");
        bankStatement.AccountHolderName.Should().Be("John Doe");
        bankStatement.Currency.Should().Be("USD");
    }

    [Fact]
    public void Transaction_ShouldCreateInstanceWithDefaultValues()
    {
        // Act
        var transaction = new Transaction();

        // Assert
        transaction.Id.Should().Be(0);
        transaction.BankStatementId.Should().Be(0);
        transaction.Description.Should().Be(string.Empty);
        transaction.Amount.Should().Be(0);
        transaction.Type.Should().Be(TransactionType.Debit);
        transaction.Balance.Should().Be(0);
    }

    [Fact]
    public void Transaction_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var transactionDate = new DateTime(2024, 1, 15);
        var bankStatement = new BankStatement();

        // Act
        var transaction = new Transaction
        {
            Id = 1,
            BankStatementId = 123,
            Date = transactionDate,
            Description = "Test Transaction",
            Amount = -50.25m,
            Type = TransactionType.Debit,
            Category = "Food",
            Reference = "REF123",
            Balance = 949.75m,
            Counterparty = "Test Store",
            Purpose = "Purchase",
            BankStatement = bankStatement
        };

        // Assert
        transaction.Id.Should().Be(1);
        transaction.BankStatementId.Should().Be(123);
        transaction.Date.Should().Be(transactionDate);
        transaction.Description.Should().Be("Test Transaction");
        transaction.Amount.Should().Be(-50.25m);
        transaction.Type.Should().Be(TransactionType.Debit);
        transaction.Category.Should().Be("Food");
        transaction.Reference.Should().Be("REF123");
        transaction.Balance.Should().Be(949.75m);
        transaction.Counterparty.Should().Be("Test Store");
        transaction.Purpose.Should().Be("Purchase");
        transaction.BankStatement.Should().Be(bankStatement);
    }

    [Fact]
    public void BankStatement_ShouldHandleNullableProperties()
    {
        // Act
        var bankStatement = new BankStatement
        {
            ProcessedAt = null,
            AccountHolderName = null,
            Currency = null
        };

        // Assert
        bankStatement.ProcessedAt.Should().BeNull();
        bankStatement.AccountHolderName.Should().BeNull();
        bankStatement.Currency.Should().BeNull();
    }

    [Fact]
    public void Transaction_ShouldHandleNullableProperties()
    {
        // Act
        var transaction = new Transaction
        {
            Category = null,
            Reference = null,
            Counterparty = null,
            Purpose = null
        };

        // Assert
        transaction.Category.Should().BeNull();
        transaction.Reference.Should().BeNull();
        transaction.Counterparty.Should().BeNull();
        transaction.Purpose.Should().BeNull();
    }

    [Fact]
    public void BankStatement_ShouldMaintainTransactionRelationship()
    {
        // Arrange
        var bankStatement = new BankStatement { Id = 1 };
        var transaction1 = new Transaction { Id = 1, BankStatementId = 1 };
        var transaction2 = new Transaction { Id = 2, BankStatementId = 1 };

        // Act
        bankStatement.Transactions.Add(transaction1);
        bankStatement.Transactions.Add(transaction2);

        // Assert
        bankStatement.Transactions.Should().HaveCount(2);
        bankStatement.Transactions.Should().Contain(transaction1);
        bankStatement.Transactions.Should().Contain(transaction2);
    }
}
