# Database Usage Guide

This document explains how to manage the SQLite database used by the Bank Statement Parsing application.

---

## Database Location

- The SQLite database file is located at:
  - `Database/bankstatements.db` (relative to the solution root)

---

## Creating and Updating the Database Schema

The database schema is managed using Entity Framework Core migrations.

### 1. **Add a Migration**
If you make changes to the entity models or DbContext, add a new migration:

```sh
dotnet ef migrations add <MigrationName> --project BankStatementParsing.Infrastructure --startup-project BankStatementParsing.TestConsole
```

### 2. **Apply Migrations (Create/Update Database)**
To create or update the database schema to the latest version:

```sh
dotnet ef database update --project BankStatementParsing.Infrastructure --startup-project BankStatementParsing.TestConsole
```

This will create all necessary tables in `Database/bankstatements.db`.

---

## Inspecting the Database

- You can open `Database/bankstatements.db` with any SQLite browser (e.g., [DB Browser for SQLite](https://sqlitebrowser.org/)).
- You can run SQL queries to inspect tables, data, and relationships.

---

## Troubleshooting

### **Error: 'no such table: Accounts'**
- This means the database file exists, but the schema has not been created.
- **Solution:** Run the database update command above to apply migrations.

### **Error: 'unable to open database file'**
- This means the file path is incorrect or the directory does not exist.
- **Solution:** Ensure the `Database` directory exists and the path is correct in your `DbContext` configuration.

---

## Best Practices

- Always run `dotnet ef database update` after pulling new migrations or changing the schema.
- Use migrations to evolve your schema, not `EnsureCreated()`.
- Back up your database file before making destructive changes.

---

For more details, see the main `Usage.md` or ask your AI assistant for help! 