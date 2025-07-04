using BankStatementParsing.Core.Models;
using BankStatementParsing.Core.Enums;
using FluentAssertions;

namespace BankStatementParsing.UnitTests;

public class BankStatementDataTests
{
    [Fact]
    public void BankStatementData_ShouldInitializeWithDefaultValues()
    {
        // Act
        var bankStatementData = new BankStatementData();

        // Assert
        bankStatementData.AccountNumber.Should().Be(string.Empty);
        bankStatementData.AccountHolderName.Should().Be(string.Empty);
        bankStatementData.BankName.Should().Be(string.Empty);
        bankStatementData.StatementPeriodStart.Should().Be(default(DateTime));
        bankStatementData.StatementPeriodEnd.Should().Be(default(DateTime));
        bankStatementData.OpeningBalance.Should().Be(0);
        bankStatementData.ClosingBalance.Should().Be(0);
        bankStatementData.Currency.Should().Be("RUB");
        bankStatementData.Transactions.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void BankStatementData_ShouldSetAllPropertiesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var transactions = new List<TransactionData>
        {
            new TransactionData { Description = "Transaction 1", Amount = 100.50m },
            new TransactionData { Description = "Transaction 2", Amount = -50.25m }
        };

        // Act
        var bankStatementData = new BankStatementData
        {
            AccountNumber = "1234567890",
            AccountHolderName = "John Doe",
            BankName = "Test Bank",
            StatementPeriodStart = startDate,
            StatementPeriodEnd = endDate,
            OpeningBalance = 1000.75m,
            ClosingBalance = 1050.00m,
            Currency = "USD",
            Transactions = transactions
        };

        // Assert
        bankStatementData.AccountNumber.Should().Be("1234567890");
        bankStatementData.AccountHolderName.Should().Be("John Doe");
        bankStatementData.BankName.Should().Be("Test Bank");
        bankStatementData.StatementPeriodStart.Should().Be(startDate);
        bankStatementData.StatementPeriodEnd.Should().Be(endDate);
        bankStatementData.OpeningBalance.Should().Be(1000.75m);
        bankStatementData.ClosingBalance.Should().Be(1050.00m);
        bankStatementData.Currency.Should().Be("USD");
        bankStatementData.Transactions.Should().HaveCount(2);
        bankStatementData.Transactions[0].Description.Should().Be("Transaction 1");
        bankStatementData.Transactions[1].Description.Should().Be("Transaction 2");
    }

    [Fact]
    public void BankStatementData_TransactionsCollection_ShouldSupportManipulation()
    {
        // Arrange
        var bankStatementData = new BankStatementData();
        var transaction1 = new TransactionData { Description = "First", Amount = 100m };
        var transaction2 = new TransactionData { Description = "Second", Amount = 200m };

        // Act
        bankStatementData.Transactions.Add(transaction1);
        bankStatementData.Transactions.Add(transaction2);

        // Assert
        bankStatementData.Transactions.Should().HaveCount(2);
        bankStatementData.Transactions.Should().Contain(transaction1);
        bankStatementData.Transactions.Should().Contain(transaction2);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("")]
    [InlineData(null)]
    public void BankStatementData_ShouldHandleDifferentCurrencies(string currency)
    {
        // Act
        var bankStatementData = new BankStatementData { Currency = currency };

        // Assert
        bankStatementData.Currency.Should().Be(currency);
    }

    [Fact]
    public void BankStatementData_ShouldHandleNegativeBalances()
    {
        // Act
        var bankStatementData = new BankStatementData
        {
            OpeningBalance = -100.50m,
            ClosingBalance = -200.75m
        };

        // Assert
        bankStatementData.OpeningBalance.Should().Be(-100.50m);
        bankStatementData.ClosingBalance.Should().Be(-200.75m);
    }
}

public class TransactionDataTests
{
    [Fact]
    public void TransactionData_ShouldInitializeWithDefaultValues()
    {
        // Act
        var transactionData = new TransactionData();

        // Assert
        transactionData.Date.Should().Be(default(DateTime));
        transactionData.Description.Should().Be(string.Empty);
        transactionData.Amount.Should().Be(0);
        transactionData.Type.Should().Be(TransactionType.Debit);
        transactionData.Category.Should().BeNull();
        transactionData.Reference.Should().BeNull();
        transactionData.Balance.Should().Be(0);
        transactionData.Counterparty.Should().BeNull();
        transactionData.Purpose.Should().BeNull();
        transactionData.MerchantName.Should().BeNull();
        transactionData.Countervalue.Should().BeNull();
        transactionData.ExchangeRate.Should().BeNull();
    }

    [Fact]
    public void TransactionData_ShouldSetAllPropertiesCorrectly()
    {
        // Arrange
        var transactionDate = new DateTime(2024, 1, 15, 14, 30, 0);

        // Act
        var transactionData = new TransactionData
        {
            Date = transactionDate,
            Description = "Test Purchase",
            Amount = -75.50m,
            Type = TransactionType.Debit,
            Category = "Shopping",
            Reference = "REF123456",
            Balance = 924.50m,
            Counterparty = "Test Store Ltd",
            Purpose = "Retail Purchase",
            MerchantName = "Test Store",
            Countervalue = 80.25m,
            ExchangeRate = 1.063m
        };

        // Assert
        transactionData.Date.Should().Be(transactionDate);
        transactionData.Description.Should().Be("Test Purchase");
        transactionData.Amount.Should().Be(-75.50m);
        transactionData.Type.Should().Be(TransactionType.Debit);
        transactionData.Category.Should().Be("Shopping");
        transactionData.Reference.Should().Be("REF123456");
        transactionData.Balance.Should().Be(924.50m);
        transactionData.Counterparty.Should().Be("Test Store Ltd");
        transactionData.Purpose.Should().Be("Retail Purchase");
        transactionData.MerchantName.Should().Be("Test Store");
        transactionData.Countervalue.Should().Be(80.25m);
        transactionData.ExchangeRate.Should().Be(1.063m);
    }

    [Theory]
    [InlineData(TransactionType.Debit)]
    [InlineData(TransactionType.Credit)]
    public void TransactionData_ShouldHandleBothTransactionTypes(TransactionType type)
    {
        // Act
        var transactionData = new TransactionData { Type = type };

        // Assert
        transactionData.Type.Should().Be(type);
    }

    [Fact]
    public void TransactionData_ShouldHandlePositiveAndNegativeAmounts()
    {
        // Arrange & Act
        var debitTransaction = new TransactionData { Amount = -100.50m, Type = TransactionType.Debit };
        var creditTransaction = new TransactionData { Amount = 200.75m, Type = TransactionType.Credit };

        // Assert
        debitTransaction.Amount.Should().Be(-100.50m);
        debitTransaction.Type.Should().Be(TransactionType.Debit);
        creditTransaction.Amount.Should().Be(200.75m);
        creditTransaction.Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public void TransactionData_ShouldHandleNullablePropertiesCorrectly()
    {
        // Act
        var transactionData = new TransactionData
        {
            Category = null,
            Reference = null,
            Counterparty = null,
            Purpose = null,
            MerchantName = null,
            Countervalue = null,
            ExchangeRate = null
        };

        // Assert
        transactionData.Category.Should().BeNull();
        transactionData.Reference.Should().BeNull();
        transactionData.Counterparty.Should().BeNull();
        transactionData.Purpose.Should().BeNull();
        transactionData.MerchantName.Should().BeNull();
        transactionData.Countervalue.Should().BeNull();
        transactionData.ExchangeRate.Should().BeNull();
    }

    [Fact]
    public void TransactionData_ShouldHandleVeryLargeAmounts()
    {
        // Act
        var transactionData = new TransactionData
        {
            Amount = 999999999.99m,
            Balance = -999999999.99m,
            Countervalue = 1234567890.12m
        };

        // Assert
        transactionData.Amount.Should().Be(999999999.99m);
        transactionData.Balance.Should().Be(-999999999.99m);
        transactionData.Countervalue.Should().Be(1234567890.12m);
    }

    [Fact]
    public void TransactionData_ShouldHandleExchangeRateCalculations()
    {
        // Arrange & Act
        var transactionData = new TransactionData
        {
            Amount = 100m,
            Countervalue = 85m,
            ExchangeRate = 0.85m
        };

        // Assert
        transactionData.Amount.Should().Be(100m);
        transactionData.Countervalue.Should().Be(85m);
        transactionData.ExchangeRate.Should().Be(0.85m);
        
        // Verify the mathematical relationship (if the exchange rate calculation is correct)
        var calculatedCountervalue = transactionData.Amount * transactionData.ExchangeRate;
        calculatedCountervalue.Should().Be(transactionData.Countervalue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Very long transaction description that might exceed normal limits but should still be handled correctly")]
    public void TransactionData_ShouldHandleDifferentDescriptionFormats(string description)
    {
        // Act
        var transactionData = new TransactionData { Description = description };

        // Assert
        transactionData.Description.Should().Be(description);
    }

    [Fact]
    public void TransactionData_ShouldHandleDifferentDateFormats()
    {
        // Arrange
        var dates = new[]
        {
            new DateTime(2024, 1, 1),
            new DateTime(2024, 12, 31, 23, 59, 59),
            DateTime.MinValue,
            DateTime.MaxValue
        };

        foreach (var date in dates)
        {
            // Act
            var transactionData = new TransactionData { Date = date };

            // Assert
            transactionData.Date.Should().Be(date);
        }
    }
}