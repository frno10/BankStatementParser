# Updated Architecture - SQLite + Mapster + File Processing

## Key Changes from Original Guide

### Database
- **Changed**: SQL Server → SQLite
- **Reason**: Lightweight, file-based, easier deployment
- **Configuration**: `Data Source=Database/BankStatements.db;Cache=Shared;`

### Object Mapping
- **Changed**: AutoMapper → Mapster
- **Reason**: Better performance, less reflection overhead
- **Benefits**: 2-3x faster mapping, lower memory usage

### Processing Model
- **Changed**: Upload-based → File drop-based
- **Workflow**: Drop PDFs in account folders → Automatic processing
- **States**: Inbox → Processing → Processed/Failed

### Logging
- **Enhanced**: Extensive Serilog logging
- **Outputs**: Console (JSON) + Files (Application/Processing/Errors)
- **Context**: Processing session IDs, file tracking, performance metrics

## Current Status Analysis

Based on your report that the app crashed, here's what we've completed and what's missing:

### ✅ Completed
1. **Folder Structure**: Account folders with Inbox/Processing/Processed/Failed
2. **Documentation**: Features.md, FileProcessing.md, DatabaseDesign.md, LoggingStrategy.md
3. **Architecture Planning**: Updated design with SQLite and Mapster

### ❌ Missing (Causing the Crash)
1. **Actual Project Files**: Projects are empty directories
2. **Database Context**: No SQLite DbContext implementation
3. **Configuration**: No appsettings.json with SQLite connection
4. **Startup Code**: No Program.cs with proper DI setup
5. **Services**: No actual service implementations

## Immediate Action Required

To fix the crash and get the application running:

### 1. Create the .NET Solution
```bash
dotnet new sln -n BankStatementParsing
```

### 2. Create Project Files
```bash
dotnet new webapi -n BankStatementParsing.Api
dotnet new classlib -n BankStatementParsing.Core  
dotnet new classlib -n BankStatementParsing.Infrastructure
dotnet new classlib -n BankStatementParsing.Services
dotnet new xunit -n BankStatementParsing.Tests
```

### 3. Add Projects to Solution
```bash
dotnet sln add BankStatementParsing.Api
dotnet sln add BankStatementParsing.Core
dotnet sln add BankStatementParsing.Infrastructure  
dotnet sln add BankStatementParsing.Services
dotnet sln add BankStatementParsing.Tests
```

### 4. Add Project References
```bash
cd BankStatementParsing.Api
dotnet add reference ../BankStatementParsing.Core
dotnet add reference ../BankStatementParsing.Infrastructure
dotnet add reference ../BankStatementParsing.Services
```

### 5. Install Required Packages
```bash
# In API project
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Serilog.AspNetCore
dotnet add package Mapster
```

### 6. Create Basic Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Database/BankStatements.db"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "Logs/app-.log" } }
    ]
  }
}
```

## Priority Implementation Order

1. **Basic API Setup** - Get the web API running
2. **SQLite DbContext** - Database connectivity  
3. **Basic Entities** - Account, BankStatement, Transaction
4. **File Watcher Service** - Monitor account folders
5. **PDF Processing** - Extract text from PDFs
6. **Mapster Configuration** - Object mapping setup
7. **Extensive Logging** - Serilog with multiple outputs

## Expected File Structure After Implementation

```
BankStatementParsing/
├── BankStatementParsing.sln
├── BankStatementParsing.Api/
│   ├── BankStatementParsing.Api.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   └── Controllers/
├── BankStatementParsing.Core/
│   ├── BankStatementParsing.Core.csproj
│   ├── Entities/
│   ├── DTOs/
│   └── Interfaces/
├── BankStatementParsing.Infrastructure/
│   ├── BankStatementParsing.Infrastructure.csproj
│   ├── Data/
│   └── Services/
└── BankStatementParsing.Services/
    ├── BankStatementParsing.Services.csproj
    └── Implementation files
```

The crash is occurring because we have empty project directories without actual .NET project files. The next step is to run the `dotnet new` commands to create the actual projects. 