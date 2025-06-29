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
dotnet run --project BankStatementParsing.TestConsole -- --force
```

- The `--force` flag can be placed after `--` to ensure it is passed to your application.

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