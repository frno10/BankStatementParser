# Bank Statement Parsing – Usage Guide

## Running the Batch Parser

The batch parser will scan all PDF files in the `AccountData/**/Inbox/` folders and import transactions into the database.

### Parse Only New PDFs

This will parse and import only those PDFs that do **not** have a corresponding `.txt` file:

```sh
dotnet run --project BankStatementParsing.TestConsole
```

### Force Parse All PDFs

This will parse and import **all** PDFs, regardless of whether a `.txt` file exists:

```sh
dotnet run --project BankStatementParsing.TestConsole --force
```
Or, you can use the universal .NET CLI pattern:
```sh
dotnet run --project BankStatementParsing.TestConsole -- --force
```
- The first `--` tells the .NET CLI to stop parsing options for itself and pass everything after it directly to your application. This is only needed if your argument could be confused with a .NET CLI option.
- For the `--force` flag, both forms work. Use whichever you prefer.

## Duplicate Prevention

- The import logic will **not** import the same transaction twice (based on date, amount, description, and statement).

## Database Location

- The SQLite database is located at: `Database/bankstatements.db`
- You can open this file with any SQLite browser to inspect imported data.

## Adding More Banks

- To support more banks, implement additional parser classes and register them in the test console.
- The current parser is generic (`PdfStatementParser`) and can be extended or replaced as needed.

## Updating This Guide

- This file should be updated as new features, flags, or workflows are added to the codebase.

---

If you have questions or need to extend the workflow, see the code in `BankStatementParsing.TestConsole/Program.cs` or ask your AI assistant for help! 

---

## Running and Using the Web Application

The web application provides a modern dashboard and advanced transaction management interface for reviewing imported bank statement data.

### Start the Web App

To run the web application:

```sh
dotnet run --project BankStatementParsing.Web
```

- The app will start on:  
  - HTTP: [http://localhost:1164](http://localhost:1164)  
  - HTTPS: [https://localhost:1165](https://localhost:1165)
- The browser should open automatically. If not, open your browser and navigate to the above URL.

### Features

- **Dashboard:**
  - Financial summary cards (accounts, credits, debits, net balance)
  - Interactive charts (monthly trends, category breakdown)
  - Account summaries, top merchants, recent transactions
- **Transactions:**
  - Advanced filtering (account, merchant, date, amount, type, description, tags, etc.)
  - Sortable, paginated transaction table
  - CSV export for filtered results
- **Analytics:**
  - (Placeholder for future advanced analytics)

### Navigation
- Use the top navigation bar to switch between Dashboard, Transactions, Analytics, and Settings.
- All pages are responsive and mobile-friendly.

### Data
- If you have already imported data using the batch parser, it will be visible in the dashboard and transaction views.
- If no data is present, the dashboard and tables will indicate that no records are available.

### Database
- The web app uses the same SQLite database: `Database/bankstatements.db`
- Any data imported via the batch parser will be immediately available in the web interface.

---

For further help or troubleshooting, see the code in `BankStatementParsing.Web/` or ask your AI assistant! 