# Bank Statement Processing CLI

A powerful command-line interface for processing bank statement PDFs with granular functionality and command chaining capabilities.

## Installation

### As a .NET Global Tool
```bash
# Build and pack the tool
dotnet pack -c Release

# Install globally
dotnet tool install --global --add-source ./nupkg BankStatementParsing.CLI
```

### Local Build
```bash
# Build the project
dotnet build -c Release

# Run directly
dotnet run -- [commands]
```

## Quick Start

```bash
# Process a single PDF file through the entire pipeline
bankstmt process --input statement.pdf --bank CSOB

# Extract text from multiple PDFs
bankstmt extract --input *.pdf --output-dir ./extracted

# Parse extracted text files
bankstmt parse --input ./extracted/*.txt --bank CSOB --output-dir ./parsed

# Import parsed data to database
bankstmt import --input ./parsed/*.json
```

## Commands

### `extract` - Extract text from PDF files

Extract text from PDF bank statements for further processing.

```bash
# Basic extraction
bankstmt extract --input statement.pdf

# Extract from directory with custom output
bankstmt extract --input ./pdfs/ --output-dir ./extracted --force

# Extract with account context
bankstmt extract --input statement.pdf --account Account1 --verbose
```

**Options:**
- `--input` (required): PDF file(s) or directory
- `--output-dir`: Output directory for text files
- `--force`: Overwrite existing text files
- `--account`: Account identifier for processing

### `parse` - Parse extracted text into structured data

Parse extracted text files into structured transaction data.

```bash
# Basic parsing
bankstmt parse --input extracted.txt --bank CSOB

# Parse with JSON output
bankstmt parse --input ./extracted/*.txt --bank CSOB --output-dir ./parsed --format json

# Parse with CSV output
bankstmt parse --input extracted.txt --bank CSOB --format csv
```

**Options:**
- `--input` (required): Text file(s) or directory
- `--bank` (required): Bank type (CSOB, etc.)
- `--output-dir`: Output directory for parsed data
- `--format`: Output format (json, csv)
- `--account`: Account identifier

### `import` - Import parsed data to database

Import structured transaction data into the database.

```bash
# Basic import
bankstmt import --input parsed.json

# Import with duplicate handling
bankstmt import --input ./parsed/*.json --skip-duplicates --account Account1

# Dry run (show what would be imported)
bankstmt import --input parsed.json --dry-run
```

**Options:**
- `--input` (required): JSON/CSV file(s) or directory
- `--account`: Account identifier
- `--skip-duplicates`: Skip duplicate transactions (default: true)
- `--dry-run`: Show what would be imported without importing

### `process` - Full pipeline processing

Run the complete pipeline: extract → parse → import.

```bash
# Full processing
bankstmt process --input statement.pdf --bank CSOB

# Process with custom working directory
bankstmt process --input ./pdfs/ --bank CSOB --work-dir ./temp --keep-intermediate

# Dry run processing
bankstmt process --input statement.pdf --bank CSOB --dry-run
```

**Options:**
- `--input` (required): PDF file(s) or directory
- `--bank` (required): Bank type
- `--account`: Account identifier
- `--work-dir`: Working directory for intermediate files
- `--keep-intermediate`: Keep intermediate files (txt, json)
- `--skip-duplicates`: Skip duplicate transactions (default: true)
- `--dry-run`: Process without importing to database

### `database` - Database management

Manage database operations.

```bash
# Show database status
bankstmt database status

# Clear transaction data (keep merchants/tags)
bankstmt database clear --confirmed

# Clear all data
bankstmt database clear --all --confirmed
```

**Sub-commands:**
- `status`: Show database statistics and file information
- `clear`: Clear database data
  - `--all`: Clear all data including merchants and tags
  - `--confirmed`: Confirm operation without prompting

## Global Options

All commands support these global options:

- `--verbose`: Enable verbose output
- `--quiet`: Suppress non-error output
- `--output`: Output format (table, json, csv)
- `--config`: Configuration file path
- `--log-level`: Logging level (Debug, Information, Warning, Error)

## Command Chaining Examples

### Basic Pipeline
```bash
# Extract → Parse → Import
bankstmt extract --input statement.pdf --output-dir ./temp
bankstmt parse --input ./temp/*.txt --bank CSOB --output-dir ./temp
bankstmt import --input ./temp/*.json
```

### Batch Processing with Shell Pipes
```bash
# Find and process all PDFs in directory structure
find ./statements -name "*.pdf" | while read pdf; do
    bankstmt process --input "$pdf" --bank CSOB --verbose
done
```

### JSON Output for Further Processing
```bash
# Extract with JSON output for external tools
bankstmt extract --input *.pdf --output json | jq '.[] | select(.Success == true)'

# Parse and get transaction count
bankstmt parse --input *.txt --bank CSOB --output json | jq 'map(.TransactionCount) | add'
```

### Conditional Processing
```bash
# Only import if parsing was successful
if bankstmt parse --input extracted.txt --bank CSOB --output-dir ./temp --quiet; then
    bankstmt import --input ./temp/*.json
else
    echo "Parsing failed, skipping import"
fi
```

### Parallel Processing
```bash
# Process multiple files in parallel
parallel bankstmt process --input {} --bank CSOB ::: *.pdf
```

## Advanced Usage

### Configuration File
Create `appsettings.json` to customize default behavior:

```json
{
  "Processing": {
    "DefaultBankType": "CSOB",
    "MaxConcurrentFiles": 3,
    "TempDirectory": "temp",
    "KeepIntermediateFiles": false
  },
  "Database": {
    "ConnectionString": "Data Source=./bankstatements.db"
  }
}
```

### Environment Variables
```bash
export BANKSTMT_BANK_TYPE=CSOB
export BANKSTMT_LOG_LEVEL=Debug
bankstmt process --input statement.pdf
```

### Batch Scripts

Create reusable scripts for common workflows:

```bash
#!/bin/bash
# process-statements.sh
BANK_TYPE=${1:-CSOB}
INPUT_DIR=${2:-./statements}

echo "Processing statements in $INPUT_DIR with bank type $BANK_TYPE"
bankstmt process \
    --input "$INPUT_DIR"/*.pdf \
    --bank "$BANK_TYPE" \
    --verbose \
    --skip-duplicates
```

## Output Formats

### Table Format (default)
```
✓ Extracted: statement.pdf -> extracted.txt
✓ Parsed: extracted.txt -> 25 transactions
✓ Imported: parsed.json -> 25 transactions
Summary: 1 successful, 0 failed, 25 transactions imported
```

### JSON Format
```bash
bankstmt process --input statement.pdf --bank CSOB --output json
```
```json
[
  {
    "InputFile": "statement.pdf",
    "Success": true,
    "TransactionCount": 25,
    "TotalDuration": "00:00:03.1234567",
    "ExtractStep": {
      "Success": true,
      "Duration": "00:00:01.2345678",
      "OutputPath": "extracted.txt"
    }
  }
]
```

### CSV Format
```bash
bankstmt extract --input *.pdf --output csv
```
```csv
File,Success,OutputPath,Error
statement1.pdf,true,statement1.txt,
statement2.pdf,false,,PDF file corrupted
```

## Error Handling

The CLI provides detailed error information and proper exit codes:

- `0`: Success
- `1`: General error or some operations failed
- `2`: Invalid arguments or configuration

```bash
# Check if processing was successful
if bankstmt process --input statement.pdf --bank CSOB; then
    echo "Processing completed successfully"
else
    echo "Processing failed with exit code $?"
fi
```

## Logging

Logs are written to both console and files:
- Console: Structured colored output
- Files: `logs/bankstmt-YYYY-MM-DD.log` with detailed information

Configure logging levels:
```bash
# Debug level for troubleshooting
bankstmt process --input statement.pdf --bank CSOB --log-level Debug

# Quiet operation
bankstmt process --input statement.pdf --bank CSOB --quiet
```

## Integration Examples

### CI/CD Pipeline
```yaml
- name: Process Bank Statements
  run: |
    bankstmt process \
      --input ./statements/*.pdf \
      --bank CSOB \
      --output json > results.json
    
    # Check if any processing failed
    if jq -e '.[] | select(.Success == false)' results.json; then
      echo "Some files failed processing"
      exit 1
    fi
```

### Monitoring Script
```bash
#!/bin/bash
# monitor-processing.sh
watch -n 30 'bankstmt database status'
```

### Data Export
```bash
# Export all processed data
bankstmt database status --output json | jq '.TransactionCount'
```

## Troubleshooting

### Common Issues

1. **PDF not readable**: Ensure PDF is not password-protected or corrupted
2. **Database locked**: Close other applications using the database
3. **Permission denied**: Check file permissions and disk space

### Debug Mode
```bash
# Enable verbose logging for troubleshooting
bankstmt process --input statement.pdf --bank CSOB --verbose --log-level Debug
```

### Reset Database
```bash
# Clear all data and start fresh
bankstmt database clear --all --confirmed
```

## Contributing

The CLI is designed to be extensible. To add new commands:

1. Create a new command class inheriting from `BaseCommand`
2. Register it in `Program.cs`
3. Add corresponding service interfaces/implementations if needed

## License

[Your License Here]