# Bank Statement Parsing Application - Development Guide

## Overview
This guide provides a step-by-step approach to building a secure, scalable bank statement parsing application using .NET 8, ASP.NET Core, and Entity Framework Core.

## Table of Contents
1. [Project Setup](#1-project-setup)
2. [Architecture Design](#2-architecture-design)
3. [Technology Stack](#3-technology-stack)
4. [Database Design](#4-database-design)
5. [Core Implementation](#5-core-implementation)
6. [File Processing](#6-file-processing)
7. [API Development](#7-api-development)
8. [Security Implementation](#8-security-implementation)
9. [Testing Strategy](#9-testing-strategy)
10. [Deployment](#10-deployment)

## 1. Project Setup

### 1.1 Initialize Solution and Projects
```bash
# Create solution
dotnet new sln -n BankStatementParsing

# Create API project
dotnet new webapi -n BankStatementParsing.Api
dotnet sln add BankStatementParsing.Api

# Create Core library (Domain models, interfaces)
dotnet new classlib -n BankStatementParsing.Core
dotnet sln add BankStatementParsing.Core

# Create Infrastructure library (Data access, external services)
dotnet new classlib -n BankStatementParsing.Infrastructure
dotnet sln add BankStatementParsing.Infrastructure

# Create Services library (Business logic)
dotnet new classlib -n BankStatementParsing.Services
dotnet sln add BankStatementParsing.Services

# Create Tests project
dotnet new xunit -n BankStatementParsing.Tests
dotnet sln add BankStatementParsing.Tests
```

### 1.2 Add Project References
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

### 1.3 Install Required NuGet Packages

#### API Project
```bash
cd BankStatementParsing.Api
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Swashbuckle.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package FluentValidation.AspNetCore
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
```

#### Core Project
```bash
cd ../BankStatementParsing.Core
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package System.ComponentModel.Annotations
```

#### Infrastructure Project
```bash
cd ../BankStatementParsing.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.Extensions.Configuration
dotnet add package iTextSharp
dotnet add package CsvHelper
```

#### Services Project
```bash
cd ../BankStatementParsing.Services
dotnet add package Microsoft.Extensions.Logging
dotnet add package FluentValidation
```

#### Tests Project
```bash
cd ../BankStatementParsing.Tests
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Moq
dotnet add package FluentAssertions
```

## 2. Architecture Design

### 2.1 Clean Architecture Principles
- **Core**: Domain entities, value objects, interfaces, enums
- **Services**: Business logic, application services, validation rules
- **Infrastructure**: Data access, external APIs, file system operations
- **API**: Controllers, DTOs, middleware, configuration

### 2.2 Key Design Patterns
- Repository Pattern for data access
- Service Layer for business logic
- Dependency Injection for loose coupling
- CQRS for read/write separation (optional)
- Strategy Pattern for different parser implementations

## 3. Technology Stack

### Primary Technologies
- **.NET 8**: Latest LTS version
- **ASP.NET Core**: Web API framework
- **Entity Framework Core**: ORM for database operations
- **SQL Server**: Primary database
- **AutoMapper**: Object-to-object mapping
- **FluentValidation**: Input validation
- **Swagger/OpenAPI**: API documentation
- **Serilog**: Structured logging
- **JWT**: Authentication and authorization

### File Processing Librariesls
- **iTextSharp**: PDF parsing
- **CsvHelper**: CSV file processing
- **System.Text.Json**: JSON serialization

## 4. Database Design

### 4.1 Core Entities

#### User Entity
```csharp
// Core/Entities/User.cs
public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<BankStatement> BankStatements { get; set; }
}
```

#### BankStatement Entity
```csharp
// Core/Entities/BankStatement.cs
public class BankStatement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FileName { get; set; }
    public string BankName { get; set; }
    public string AccountNumber { get; set; }
    public DateTime StatementPeriodStart { get; set; }
    public DateTime StatementPeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public ProcessingStatus Status { get; set; }
    public string FilePath { get; set; }
    public User User { get; set; }
    public List<Transaction> Transactions { get; set; }
}
```

#### Transaction Entity
```csharp
// Core/Entities/Transaction.cs
public class Transaction
{
    public int Id { get; set; }
    public int BankStatementId { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Category { get; set; }
    public string Reference { get; set; }
    public decimal Balance { get; set; }
    public BankStatement BankStatement { get; set; }
}
```

### 4.2 Enums
```csharp
// Core/Enums/ProcessingStatus.cs
public enum ProcessingStatus
{
    Uploaded,
    Processing,
    Completed,
    Failed
}

// Core/Enums/TransactionType.cs
public enum TransactionType
{
    Debit,
    Credit
}
```

## 5. Core Implementation

### 5.1 Define Interfaces

#### Repository Interfaces
```csharp
// Core/Interfaces/IRepository.cs
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

// Core/Interfaces/IBankStatementRepository.cs
public interface IBankStatementRepository : IRepository<BankStatement>
{
    Task<IEnumerable<BankStatement>> GetByUserIdAsync(int userId);
    Task<BankStatement> GetWithTransactionsAsync(int id);
}
```

#### Service Interfaces
```csharp
// Core/Interfaces/IFileParserService.cs
public interface IFileParserService
{
    Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName);
    bool CanParse(string fileName, string bankName);
}

// Core/Interfaces/IBankStatementService.cs
public interface IBankStatementService
{
    Task<BankStatement> UploadStatementAsync(int userId, IFormFile file, string bankName);
    Task<BankStatement> GetStatementAsync(int id);
    Task<IEnumerable<BankStatement>> GetUserStatementsAsync(int userId);
    Task ProcessStatementAsync(int statementId);
}
```

### 5.2 Data Transfer Objects
```csharp
// Core/DTOs/BankStatementUploadDto.cs
public class BankStatementUploadDto
{
    public string BankName { get; set; }
    public IFormFile File { get; set; }
}

// Core/DTOs/BankStatementDto.cs
public class BankStatementDto
{
    public int Id { get; set; }
    public string FileName { get; set; }
    public string BankName { get; set; }
    public string AccountNumber { get; set; }
    public DateTime StatementPeriodStart { get; set; }
    public DateTime StatementPeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public ProcessingStatus Status { get; set; }
    public List<TransactionDto> Transactions { get; set; }
}

// Core/DTOs/TransactionDto.cs
public class TransactionDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Category { get; set; }
    public decimal Balance { get; set; }
}
```

## 6. File Processing

### 6.1 Base Parser Class
```csharp
// Services/Parsers/BaseFileParser.cs
public abstract class BaseFileParser : IFileParserService
{
    protected readonly ILogger<BaseFileParser> _logger;

    protected BaseFileParser(ILogger<BaseFileParser> logger)
    {
        _logger = logger;
    }

    public abstract Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName);
    public abstract bool CanParse(string fileName, string bankName);

    protected virtual decimal ParseAmount(string amountText)
    {
        // Common amount parsing logic
        amountText = amountText.Replace(",", "").Replace("$", "").Trim();
        return decimal.TryParse(amountText, out var amount) ? amount : 0;
    }

    protected virtual DateTime ParseDate(string dateText)
    {
        // Common date parsing logic with multiple format support
        var formats = new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateText, format, null, DateTimeStyles.None, out var date))
                return date;
        }
        return DateTime.MinValue;
    }
}
```

### 6.2 PDF Parser Implementation
```csharp
// Services/Parsers/PdfBankStatementParser.cs
public class PdfBankStatementParser : BaseFileParser
{
    public PdfBankStatementParser(ILogger<PdfBankStatementParser> logger) : base(logger) { }

    public override bool CanParse(string fileName, string bankName)
    {
        return Path.GetExtension(fileName).ToLower() == ".pdf";
    }

    public override async Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName)
    {
        try
        {
            var text = ExtractTextFromPdf(fileStream);
            return bankName.ToLower() switch
            {
                "chase" => ParseChaseStatement(text),
                "bankofamerica" => ParseBankOfAmericaStatement(text),
                "wells" => ParseWellsFargoStatement(text),
                _ => ParseGenericStatement(text)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PDF statement {FileName}", fileName);
            throw;
        }
    }

    private string ExtractTextFromPdf(Stream pdfStream)
    {
        // Implementation using iTextSharp
        // Extract text from PDF
    }

    private BankStatementData ParseChaseStatement(string text)
    {
        // Chase-specific parsing logic
    }

    // Additional bank-specific parsing methods...
}
```

### 6.3 CSV Parser Implementation
```csharp
// Services/Parsers/CsvBankStatementParser.cs
public class CsvBankStatementParser : BaseFileParser
{
    public CsvBankStatementParser(ILogger<CsvBankStatementParser> logger) : base(logger) { }

    public override bool CanParse(string fileName, string bankName)
    {
        return Path.GetExtension(fileName).ToLower() == ".csv";
    }

    public override async Task<BankStatementData> ParseAsync(Stream fileStream, string fileName, string bankName)
    {
        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        // Configure CSV reading based on bank format
        ConfigureCsvReader(csv, bankName);
        
        var transactions = new List<TransactionData>();
        var records = csv.GetRecords<dynamic>();
        
        foreach (var record in records)
        {
            var transaction = ParseCsvRecord(record, bankName);
            if (transaction != null)
                transactions.Add(transaction);
        }
        
        return new BankStatementData
        {
            Transactions = transactions,
            // Extract other statement data from CSV headers or metadata
        };
    }

    private void ConfigureCsvReader(CsvReader csv, string bankName)
    {
        // Bank-specific CSV configuration
    }

    private TransactionData ParseCsvRecord(dynamic record, string bankName)
    {
        // Bank-specific CSV record parsing
    }
}
```

## 7. API Development

### 7.1 Controllers

#### Authentication Controller
```csharp
// Api/Controllers/AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;

    public AuthController(IUserService userService, ITokenService tokenService)
    {
        _userService = userService;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        var user = await _userService.CreateUserAsync(registerDto);
        var token = _tokenService.GenerateToken(user);
        return Ok(new { Token = token, User = user });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var user = await _userService.AuthenticateAsync(loginDto.Email, loginDto.Password);
        if (user == null)
            return Unauthorized();

        var token = _tokenService.GenerateToken(user);
        return Ok(new { Token = token, User = user });
    }
}
```

#### Bank Statements Controller
```csharp
// Api/Controllers/BankStatementsController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BankStatementsController : ControllerBase
{
    private readonly IBankStatementService _bankStatementService;
    private readonly IMapper _mapper;

    public BankStatementsController(IBankStatementService bankStatementService, IMapper mapper)
    {
        _bankStatementService = bankStatementService;
        _mapper = mapper;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadStatement([FromForm] BankStatementUploadDto uploadDto)
    {
        var userId = GetCurrentUserId();
        var statement = await _bankStatementService.UploadStatementAsync(userId, uploadDto.File, uploadDto.BankName);
        var statementDto = _mapper.Map<BankStatementDto>(statement);
        return CreatedAtAction(nameof(GetStatement), new { id = statement.Id }, statementDto);
    }

    [HttpGet]
    public async Task<IActionResult> GetStatements()
    {
        var userId = GetCurrentUserId();
        var statements = await _bankStatementService.GetUserStatementsAsync(userId);
        var statementDtos = _mapper.Map<IEnumerable<BankStatementDto>>(statements);
        return Ok(statementDtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatement(int id)
    {
        var statement = await _bankStatementService.GetStatementAsync(id);
        if (statement == null || statement.UserId != GetCurrentUserId())
            return NotFound();

        var statementDto = _mapper.Map<BankStatementDto>(statement);
        return Ok(statementDto);
    }

    [HttpPost("{id}/process")]
    public async Task<IActionResult> ProcessStatement(int id)
    {
        await _bankStatementService.ProcessStatementAsync(id);
        return Ok();
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }
}
```

### 7.2 Middleware

#### Global Exception Handling Middleware
```csharp
// Api/Middleware/ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = exception switch
        {
            ValidationException validationEx => new { 
                StatusCode = 400, 
                Message = "Validation failed", 
                Errors = validationEx.Errors 
            },
            UnauthorizedAccessException => new { 
                StatusCode = 401, 
                Message = "Unauthorized access" 
            },
            FileNotFoundException => new { 
                StatusCode = 404, 
                Message = "File not found" 
            },
            _ => new { 
                StatusCode = 500, 
                Message = "An internal server error occurred" 
            }
        };

        context.Response.StatusCode = response.StatusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

## 8. Security Implementation

### 8.1 JWT Configuration
```csharp
// Api/Services/TokenService.cs
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

### 8.2 File Security
```csharp
// Services/FileValidationService.cs
public class FileValidationService : IFileValidationService
{
    private readonly string[] _allowedExtensions = { ".pdf", ".csv", ".txt" };
    private readonly long _maxFileSize = 10 * 1024 * 1024; // 10 MB

    public ValidationResult ValidateFile(IFormFile file)
    {
        var errors = new List<string>();

        // Check file size
        if (file.Length > _maxFileSize)
            errors.Add($"File size cannot exceed {_maxFileSize / 1024 / 1024} MB");

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!_allowedExtensions.Contains(extension))
            errors.Add($"File type {extension} is not allowed");

        // Check for malicious content (basic)
        if (HasMaliciousContent(file))
            errors.Add("File contains potentially malicious content");

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }

    private bool HasMaliciousContent(IFormFile file)
    {
        // Basic malicious content detection
        // In production, use more sophisticated virus scanning
        return false;
    }
}
```

## 9. Testing Strategy

### 9.1 Unit Tests
```csharp
// Tests/Services/BankStatementServiceTests.cs
public class BankStatementServiceTests
{
    private readonly Mock<IBankStatementRepository> _mockRepository;
    private readonly Mock<IFileParserService> _mockParser;
    private readonly BankStatementService _service;

    public BankStatementServiceTests()
    {
        _mockRepository = new Mock<IBankStatementRepository>();
        _mockParser = new Mock<IFileParserService>();
        _service = new BankStatementService(_mockRepository.Object, _mockParser.Object);
    }

    [Fact]
    public async Task UploadStatementAsync_ValidFile_ReturnsStatement()
    {
        // Arrange
        var file = CreateMockFile();
        var expectedStatement = new BankStatement { Id = 1, UserId = 1 };
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<BankStatement>()))
                      .ReturnsAsync(expectedStatement);

        // Act
        var result = await _service.UploadStatementAsync(1, file, "chase");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
    }

    private IFormFile CreateMockFile()
    {
        var fileMock = new Mock<IFormFile>();
        var content = "Mock file content";
        var fileName = "test.pdf";
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;
        
        fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
        fileMock.Setup(_ => _.FileName).Returns(fileName);
        fileMock.Setup(_ => _.Length).Returns(ms.Length);
        
        return fileMock.Object;
    }
}
```

### 9.2 Integration Tests
```csharp
// Tests/Controllers/BankStatementsControllerTests.cs
public class BankStatementsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public BankStatementsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetStatements_Authenticated_ReturnsStatements()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/bankstatements");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> GetAuthTokenAsync()
    {
        // Implementation to get auth token for testing
        return "mock-token";
    }
}
```

## 10. Deployment

### 10.1 Configuration Files

#### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BankStatementParsingDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "your-super-secret-key-here-minimum-256-bits",
    "Issuer": "BankStatementParsing",
    "Audience": "BankStatementParsing"
  },
  "FileStorage": {
    "UploadPath": "uploads",
    "MaxFileSizeMB": 10
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

#### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BankStatementParsing.Api/BankStatementParsing.Api.csproj", "BankStatementParsing.Api/"]
COPY ["BankStatementParsing.Core/BankStatementParsing.Core.csproj", "BankStatementParsing.Core/"]
COPY ["BankStatementParsing.Infrastructure/BankStatementParsing.Infrastructure.csproj", "BankStatementParsing.Infrastructure/"]
COPY ["BankStatementParsing.Services/BankStatementParsing.Services.csproj", "BankStatementParsing.Services/"]

RUN dotnet restore "BankStatementParsing.Api/BankStatementParsing.Api.csproj"
COPY . .
WORKDIR "/src/BankStatementParsing.Api"
RUN dotnet build "BankStatementParsing.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BankStatementParsing.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BankStatementParsing.Api.dll"]
```

### 10.2 Startup Configuration
```csharp
// Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// Register services
builder.Services.AddScoped<IBankStatementService, BankStatementService>();
builder.Services.AddScoped<IFileParserService, PdfBankStatementParser>();
builder.Services.AddScoped<IFileParserService, CsvBankStatementParser>();
builder.Services.AddScoped<ITokenService, TokenService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Logging
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

app.Run();
```

## Next Steps

1. **Start with Project Setup**: Follow steps 1.1-1.3 to create the solution structure
2. **Implement Core Entities**: Create the domain models and interfaces
3. **Database Setup**: Configure Entity Framework and create migrations
4. **Basic API**: Implement authentication and basic CRUD operations
5. **File Parsing**: Start with one bank format (e.g., CSV) and expand
6. **Testing**: Add unit and integration tests as you build
7. **Security**: Implement proper authentication and file validation
8. **Deployment**: Configure for your target environment

## Additional Considerations

- **Performance**: Implement background processing for large files
- **Monitoring**: Add application insights and health checks
- **Scalability**: Consider using Azure Service Bus or similar for file processing queues
- **Compliance**: Ensure PCI DSS compliance for financial data handling
- **Backup**: Implement database backup and disaster recovery procedures 