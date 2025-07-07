# Update packages to latest stable versions
Write-Host "Updating packages to latest stable versions..." -ForegroundColor Green

# Update Entity Framework Core packages
Write-Host "Updating Entity Framework Core packages..." -ForegroundColor Yellow
dotnet add BankStatementParsing.Api/BankStatementParsing.Api.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.6
dotnet add BankStatementParsing.Infrastructure/BankStatementParsing.Infrastructure.csproj package Microsoft.EntityFrameworkCore --version 9.0.6
dotnet add BankStatementParsing.Infrastructure/BankStatementParsing.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 9.0.6
dotnet add BankStatementParsing.Infrastructure/BankStatementParsing.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.6
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Microsoft.EntityFrameworkCore.Design --version 9.0.6
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.6
dotnet add BankStatementParsing.IntegrationTests/BankStatementParsing.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 9.0.6

# Update Serilog packages
Write-Host "Updating Serilog packages..." -ForegroundColor Yellow
dotnet add BankStatementParsing.Api/BankStatementParsing.Api.csproj package Serilog.AspNetCore --version 9.0.0
dotnet add BankStatementParsing.Api/BankStatementParsing.Api.csproj package Serilog.Sinks.Console --version 6.0.0
dotnet add BankStatementParsing.Api/BankStatementParsing.Api.csproj package Serilog.Sinks.File --version 7.0.0
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Serilog.AspNetCore --version 9.0.0
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Serilog.Sinks.Console --version 6.0.0
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Serilog.Sinks.File --version 7.0.0
dotnet add BankStatementParsing.CLI/BankStatementParsing.CLI.csproj package Serilog.Extensions.Hosting --version 9.0.0
dotnet add BankStatementParsing.CLI/BankStatementParsing.CLI.csproj package Serilog.Settings.Configuration --version 9.0.0
dotnet add BankStatementParsing.CLI/BankStatementParsing.CLI.csproj package Serilog.Sinks.File --version 7.0.0

# Update Microsoft Extensions packages
Write-Host "Updating Microsoft Extensions packages..." -ForegroundColor Yellow
dotnet add BankStatementParsing.Infrastructure/BankStatementParsing.Infrastructure.csproj package Microsoft.Extensions.Hosting --version 9.0.6
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Microsoft.Extensions.Hosting --version 9.0.6
dotnet add BankStatementParsing.Web/BankStatementParsing.Web.csproj package Microsoft.AspNetCore.SignalR.Client --version 9.0.6

# Update test packages
Write-Host "Updating test packages..." -ForegroundColor Yellow
dotnet add BankStatementParsing.UnitTests/BankStatementParsing.UnitTests.csproj package coverlet.collector --version 6.0.4
dotnet add BankStatementParsing.UnitTests/BankStatementParsing.UnitTests.csproj package Microsoft.NET.Test.Sdk --version 17.14.1
dotnet add BankStatementParsing.UnitTests/BankStatementParsing.UnitTests.csproj package xunit --version 2.9.3
dotnet add BankStatementParsing.UnitTests/BankStatementParsing.UnitTests.csproj package xunit.runner.visualstudio --version 3.1.1
dotnet add BankStatementParsing.IntegrationTests/BankStatementParsing.IntegrationTests.csproj package coverlet.collector --version 6.0.4
dotnet add BankStatementParsing.IntegrationTests/BankStatementParsing.IntegrationTests.csproj package Microsoft.NET.Test.Sdk --version 17.14.1
dotnet add BankStatementParsing.IntegrationTests/BankStatementParsing.IntegrationTests.csproj package xunit --version 2.9.3
dotnet add BankStatementParsing.IntegrationTests/BankStatementParsing.IntegrationTests.csproj package xunit.runner.visualstudio --version 3.1.1
dotnet add BankStatementParsing.IntegrationTests/BankStatementParsing.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 9.0.6

# Update other packages
Write-Host "Updating other packages..." -ForegroundColor Yellow
dotnet add BankStatementParsing.Services/BankStatementParsing.Services.csproj package EPPlus --version 8.0.7
dotnet add BankStatementParsing.Services/BankStatementParsing.Services.csproj package NCrontab --version 3.3.3
dotnet add BankStatementParsing.Infrastructure/BankStatementParsing.Infrastructure.csproj package PdfPig --version 0.1.11-alpha-20250706-daaac

Write-Host "Package updates completed!" -ForegroundColor Green
Write-Host "Run 'dotnet build' to ensure everything compiles correctly." -ForegroundColor Cyan 