# BankStatementParsing

[![Build Status](https://github.com/frno10/BankStatementParser/actions/workflows/dotnet.yml/badge.svg)](https://github.com/frno10/BankStatementParser/actions)
[![Tests](https://github.com/frno10/BankStatementParser/actions/workflows/test.yml/badge.svg)](https://github.com/frno10/BankStatementParser/actions)
[![.NET](https://img.shields.io/badge/.NET-6.0%2B-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/frno10/BankStatementParser)](LICENSE)
[![codecov](https://codecov.io/gh/frno10/BankStatementParser/branch/main/graph/badge.svg)](https://codecov.io/gh/frno10/BankStatementParser)

---

## Overview

**BankStatementParsing** is a modular .NET solution for parsing, importing, and analyzing bank statements from various sources. It supports extensible parsing logic, robust data processing, and integration with web and CLI interfaces.

## Features
- Modular parser architecture for multiple bank formats
- CLI and Web interfaces
- Entity Framework Core for data persistence
- Background processing and scheduling
- Notification and export services
- RESTful API with versioning and Swagger documentation
- Integration and unit tests

## Quick Start

```bash
# Clone the repository
git clone https://github.com/frno10/BankStatementParser.git
cd BankStatementParser

# Build the solution
dotnet build

# Run the API project
dotnet run --project BankStatementParsing.Api/BankStatementParsing.Api.csproj

# Run the CLI
dotnet run --project BankStatementParsing.CLI/BankStatementParsing.CLI.csproj

# Run tests
dotnet test
```

## Directory Structure

- `BankStatementParsing.Api/` - ASP.NET Core Web API
- `BankStatementParsing.CLI/` - Command-line interface
- `BankStatementParsing.Core/` - Core models, entities, and interfaces
- `BankStatementParsing.Infrastructure/` - EF Core context and migrations
- `BankStatementParsing.Services/` - Business logic and parsing services
- `BankStatementParsing.Web/` - Web frontend (MVC)
- `Documentation/` - Architecture, guides, and feature docs

## Documentation

See the [Documentation Table of Contents](Documentation/README.md) for detailed guides and architecture docs.

- [Architecture Overview](Documentation/Architecture/UpdatedArchitecture.md)
- [Database Design](Documentation/Functionality/DatabaseDesign.md)
- [File Processing](Documentation/Functionality/FileProcessing.md)
- [Logging Strategy](Documentation/Functionality/LoggingStrategy.md)
- [Feature Summaries](Enhanced_Features_Implementation_Summary.md)
- [Usage Guide](Usage.md)
- [Configuration Guide](Configuration_Guide.md)
- [Testing Guide](Testing_Implementation_Summary.md)

> **Note:** The documentation table of contents can be regenerated using the provided script in `Documentation/generate-toc.ps1` (PowerShell) or `Documentation/generate-toc.sh` (Bash).

## Code Coverage

[![codecov](https://codecov.io/gh/frno10/BankStatementParser/branch/main/graph/badge.svg)](https://codecov.io/gh/frno10/BankStatementParser)

To enable code coverage reporting:
1. Add Coverlet to your test project:
   ```bash
   dotnet add package coverlet.collector
   ```
2. Run tests with coverage:
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```
3. Upload to Codecov (see [Codecov docs](https://docs.codecov.com/docs/codecov-uploader) for setup).

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details. 