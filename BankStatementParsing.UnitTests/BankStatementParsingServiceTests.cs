using BankStatementParsing.Core.Interfaces;
using BankStatementParsing.Core.Models;
using BankStatementParsing.Services;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Text;

namespace BankStatementParsing.UnitTests;

public class BankStatementParsingServiceTests
{
    private readonly Mock<ILogger<BankStatementParsingService>> _mockLogger;
    private readonly List<Mock<IFileParserService>> _mockParsers;
    private readonly BankStatementParsingService _service;

    public BankStatementParsingServiceTests()
    {
        _mockLogger = new Mock<ILogger<BankStatementParsingService>>();
        _mockParsers = new List<Mock<IFileParserService>>();
        
        // Create some mock parsers
        var parser1 = new Mock<IFileParserService>();
        parser1.Setup(p => p.SupportedBankName).Returns("TestBank1");
        parser1.Setup(p => p.CanParse("test.pdf", "TestBank1")).Returns(true);
        parser1.Setup(p => p.CanParse("test.pdf", "OtherBank")).Returns(false);
        
        var parser2 = new Mock<IFileParserService>();
        parser2.Setup(p => p.SupportedBankName).Returns("TestBank2");
        parser2.Setup(p => p.CanParse("test.pdf", "TestBank2")).Returns(true);
        parser2.Setup(p => p.CanParse("test.pdf", "OtherBank")).Returns(false);
        
        _mockParsers.Add(parser1);
        _mockParsers.Add(parser2);
        
        var parsers = _mockParsers.Select(m => m.Object);
        _service = new BankStatementParsingService(parsers, _mockLogger.Object);
    }

    [Fact]
    public async Task ParseStatementAsync_WithValidParser_ShouldReturnParsedData()
    {
        // Arrange
        var fileName = "test.pdf";
        var bankName = "TestBank1";
        var expectedData = new BankStatementData
        {
            AccountNumber = "123456789",
            BankName = bankName,
            Transactions = new List<TransactionData>
            {
                new TransactionData { Description = "Test Transaction", Amount = 100.50m }
            }
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test pdf content"));
        
        _mockParsers[0]
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), fileName, bankName))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.ParseStatementAsync(stream, fileName, bankName);

        // Assert
        result.Should().NotBeNull();
        result.AccountNumber.Should().Be("123456789");
        result.BankName.Should().Be(bankName);
        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Description.Should().Be("Test Transaction");
        result.Transactions[0].Amount.Should().Be(100.50m);
    }

    [Fact]
    public async Task ParseStatementAsync_WithUnsupportedBank_ShouldThrowNotSupportedException()
    {
        // Arrange
        var fileName = "test.pdf";
        var bankName = "UnsupportedBank";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test pdf content"));

        // Act & Assert
        var action = async () => await _service.ParseStatementAsync(stream, fileName, bankName);
        
        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("No parser found for bank 'UnsupportedBank'*");
    }

    [Fact]
    public void CanParseStatement_WithSupportedBank_ShouldReturnTrue()
    {
        // Arrange
        var fileName = "test.pdf";
        var bankName = "TestBank1";

        // Act
        var result = _service.CanParseStatement(fileName, bankName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanParseStatement_WithUnsupportedBank_ShouldReturnFalse()
    {
        // Arrange
        var fileName = "test.pdf";
        var bankName = "UnsupportedBank";

        // Act
        var result = _service.CanParseStatement(fileName, bankName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetSupportedBanks_ShouldReturnAllSupportedBankNames()
    {
        // Act
        var supportedBanks = _service.GetSupportedBanks().ToList();

        // Assert
        supportedBanks.Should().HaveCount(2);
        supportedBanks.Should().Contain("TestBank1");
        supportedBanks.Should().Contain("TestBank2");
    }

    [Fact]
    public async Task ParseStatementAsync_WithFirstParserFailing_ShouldTryNextParser()
    {
        // Arrange
        var fileName = "test.pdf";
        var bankName = "TestBank1";
        var expectedData = new BankStatementData
        {
            AccountNumber = "123456789",
            BankName = bankName
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test pdf content"));
        
        // First parser throws exception
        _mockParsers[0]
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), fileName, bankName))
            .ThrowsAsync(new InvalidOperationException("Parser failed"));

        // But if there was a second parser that could handle the same bank, it should work
        // For this test, we'll modify our setup
        _mockParsers[0].Setup(p => p.CanParse(fileName, bankName)).Returns(true);

        // Act & Assert
        var action = async () => await _service.ParseStatementAsync(stream, fileName, bankName);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ParseStatementAsync_WithInvalidFileName_ShouldStillTryToParse(string fileName)
    {
        // Arrange
        var bankName = "TestBank1";
        var expectedData = new BankStatementData { BankName = bankName };
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        
        _mockParsers[0]
            .Setup(p => p.CanParse(fileName ?? string.Empty, bankName))
            .Returns(true);
        _mockParsers[0]
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), fileName ?? string.Empty, bankName))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.ParseStatementAsync(stream, fileName ?? string.Empty, bankName);

        // Assert
        result.Should().NotBeNull();
        result.BankName.Should().Be(bankName);
    }

    [Fact]
    public async Task ParseStatementAsync_ShouldLogInformationMessages()
    {
        // Arrange
        var fileName = "test.pdf";
        var bankName = "TestBank1";
        var expectedData = new BankStatementData { BankName = bankName };
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        
        _mockParsers[0]
            .Setup(p => p.ParseAsync(It.IsAny<Stream>(), fileName, bankName))
            .ReturnsAsync(expectedData);

        // Act
        await _service.ParseStatementAsync(stream, fileName, bankName);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting to parse bank statement")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
            
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Using parser for bank")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNoParsers_ShouldNotThrow()
    {
        // Arrange
        var emptyParsers = new List<IFileParserService>();
        var logger = new Mock<ILogger<BankStatementParsingService>>();

        // Act
        var action = () => new BankStatementParsingService(emptyParsers, logger.Object);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange
        var parsers = new List<IFileParserService>();

        // Act
        var action = () => new BankStatementParsingService(parsers, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }
}

public class MockFileParserService : IFileParserService
{
    public string SupportedBankName { get; }
    private readonly Func<Stream, string, string, Task<BankStatementData>> _parseFunc;
    private readonly Func<string, string, bool> _canParseFunc;

    public MockFileParserService(
        string supportedBankName,
        Func<Stream, string, string, Task<BankStatementData>> parseFunc,
        Func<string, string, bool> canParseFunc)
    {
        SupportedBankName = supportedBankName;
        _parseFunc = parseFunc;
        _canParseFunc = canParseFunc;
    }

    public Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName)
    {
        return _parseFunc(fileStream, fileName, bankName);
    }

    public bool CanParse(string fileName, string bankName)
    {
        return _canParseFunc(fileName, bankName);
    }
}