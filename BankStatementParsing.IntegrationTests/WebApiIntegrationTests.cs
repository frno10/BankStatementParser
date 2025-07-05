using BankStatementParsing.Infrastructure;
using BankStatementParsing.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Text.Json;

namespace BankStatementParsing.IntegrationTests;

// Custom WebApplicationFactory to properly configure in-memory database
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove the DbContext registration from the main application
            services.RemoveAll(typeof(DbContextOptions<BankStatementParsingContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(BankStatementParsingContext));

            // Add in-memory database for testing with consistent name
            services.AddDbContext<BankStatementParsingContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName: "IntegrationTestDb");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });
        });
    }
}

public class WebApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly BankStatementParsingContext _context;

    public WebApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        
        // Get the context from the service provider
        using var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Get_Dashboard_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_DashboardIndex_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/Dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_Transactions_ShouldReturnOk()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _client.GetAsync("/Transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
        content.Should().Contain("Transactions"); // Should contain some reference to transactions
    }

    [Fact]
    public async Task Get_Analytics_ShouldReturnOk()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _client.GetAsync("/Analytics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_TransactionsDetails_WithValidId_ShouldReturnOk()
    {
        // Arrange
        var testData = await SeedTestDataAsync();
        var transactionId = testData.Transactions.First().Id;

        // Act
        var response = await _client.GetAsync($"/Transactions/Details/{transactionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_TransactionsDetails_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/Transactions/Details/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_BankStatementsWithTransactions_ShouldReturnTransactionsInResponse()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _client.GetAsync("/Transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
        
        // Should contain transaction details
        content.Should().Contain("Test Transaction 1");
        content.Should().Contain("Test Transaction 2");
    }

    [Fact]
    public async Task Dashboard_ShouldDisplayProcessingStatistics()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _client.GetAsync("/Dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
        
        // Should show some statistics about processed statements
        content.Should().Contain("Bank"); // Should reference bank statements
    }

    [Fact]
    public async Task Analytics_ShouldShowTransactionAnalytics()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _client.GetAsync("/Analytics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Dashboard")]
    [InlineData("/Transactions")]
    [InlineData("/Analytics")]
    public async Task Get_PublicEndpoints_ShouldReturnSuccessStatusCodes(string url)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Application_ShouldHandleConcurrentRequests()
    {
        // Arrange
        await SeedTestDataAsync();
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/Dashboard"));
            tasks.Add(_client.GetAsync("/Transactions"));
            tasks.Add(_client.GetAsync("/Analytics"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(response =>
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        });
    }

    [Fact]
    public async Task Get_NonExistentRoute_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/NonExistentRoute");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Application_ShouldHandleInvalidParameters()
    {
        // Act
        var responses = new[]
        {
            await _client.GetAsync("/Transactions/Details/invalid"),
            await _client.GetAsync("/Transactions/Details/-1"),
            await _client.GetAsync("/Transactions/Details/abc123")
        };

        // Assert
        responses.Should().AllSatisfy(response =>
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        });
    }

    private async Task<(Statement Statement, List<Transaction> Transactions)> SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();

        var account = new Account
        {
            AccountNumber = "1234567890",
            Name = "Test Account",
            Holder = "John Doe",
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

        var transactions = new List<Transaction>
        {
            new Transaction
            {
                Statement = statement,
                Date = new DateTime(2024, 1, 15),
                Description = "Test Transaction 1",
                Amount = -50.25,
                Currency = "USD",
                Reference = "REF123"
            },
            new Transaction
            {
                Statement = statement,
                Date = new DateTime(2024, 1, 20),
                Description = "Test Transaction 2",
                Amount = 100.00,
                Currency = "USD",
                Reference = "REF456"
            }
        };

        statement.Transactions = transactions;

        context.Accounts.Add(account);
        context.Statements.Add(statement);
        await context.SaveChangesAsync();

        return (statement, transactions);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }
}

public class ApiHealthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiHealthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Application_ShouldStart_WithoutErrors()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Application_ShouldServeStaticFiles()
    {
        // Act - Try to get CSS files (if they exist)
        var cssResponse = await _client.GetAsync("/css/site.css");
        var jsResponse = await _client.GetAsync("/js/site.js");

        // Assert - Should either exist (OK) or not exist (NotFound), but not have server errors
        cssResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        jsResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Application_ShouldRespectContentTypes()
    {
        // Act
        var htmlResponse = await _client.GetAsync("/");

        // Assert
        htmlResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        
        if (htmlResponse.StatusCode == HttpStatusCode.OK)
        {
            var contentType = htmlResponse.Content.Headers.ContentType?.MediaType;
            contentType.Should().Be("text/html");
        }
    }
}