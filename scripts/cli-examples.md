# CLI Examples and Patterns

This document demonstrates various ways to use the Bank Statement Processing CLI with command chaining and automation.

## Basic Commands

### Extract Text from PDFs
```bash
# Single file
bankstmt extract --input statement.pdf

# Multiple files
bankstmt extract --input statement1.pdf statement2.pdf

# Directory of files
bankstmt extract --input ./statements/

# With custom output directory
bankstmt extract --input *.pdf --output-dir ./extracted --force
```

### Parse Text Files
```bash
# Basic parsing
bankstmt parse --input extracted.txt --bank CSOB

# Batch parsing with output
bankstmt parse --input ./extracted/*.txt --bank CSOB --output-dir ./parsed

# CSV output format
bankstmt parse --input extracted.txt --bank CSOB --format csv
```

### Import to Database
```bash
# Basic import
bankstmt import --input parsed.json

# Dry run to preview
bankstmt import --input *.json --dry-run

# Skip duplicates with account context
bankstmt import --input ./parsed/*.json --account Account1 --skip-duplicates
```

### Full Pipeline
```bash
# Single command processing
bankstmt process --input statement.pdf --bank CSOB

# Batch processing with options
bankstmt process --input ./statements/*.pdf --bank CSOB --keep-intermediate --verbose
```

## Command Chaining Patterns

### 1. Sequential Processing
```bash
#!/bin/bash
# Process files step by step for maximum control

WORK_DIR="./temp"
mkdir -p "$WORK_DIR"

# Step 1: Extract
bankstmt extract --input *.pdf --output-dir "$WORK_DIR" --force

# Step 2: Parse (only if extraction succeeded)
if [ $? -eq 0 ]; then
    bankstmt parse --input "$WORK_DIR"/*.txt --bank CSOB --output-dir "$WORK_DIR"
    
    # Step 3: Import (only if parsing succeeded)
    if [ $? -eq 0 ]; then
        bankstmt import --input "$WORK_DIR"/*.json --skip-duplicates
    fi
fi
```

### 2. Parallel Processing
```bash
#!/bin/bash
# Process multiple files in parallel

export -f process_file
process_file() {
    local pdf_file="$1"
    local bank_type="$2"
    echo "Processing: $(basename "$pdf_file")"
    bankstmt process --input "$pdf_file" --bank "$bank_type" --quiet
}

# Process all PDFs in parallel (max 4 concurrent)
find ./statements -name "*.pdf" | parallel -j4 process_file {} CSOB
```

### 3. Conditional Processing
```bash
#!/bin/bash
# Only process files that haven't been processed

for pdf_file in ./statements/*.pdf; do
    json_file="./parsed/$(basename "${pdf_file%.pdf}.json")"
    
    # Skip if already processed
    if [ -f "$json_file" ]; then
        echo "Skipping $(basename "$pdf_file") - already processed"
        continue
    fi
    
    # Process new file
    bankstmt process --input "$pdf_file" --bank CSOB
done
```

## Output Format Examples

### JSON Output for Automation
```bash
# Get processing results as JSON
bankstmt process --input statement.pdf --bank CSOB --output json > results.json

# Parse JSON with jq
TRANSACTION_COUNT=$(jq '.[0].TransactionCount' results.json)
SUCCESS=$(jq '.[0].Success' results.json)

if [ "$SUCCESS" = "true" ]; then
    echo "Successfully processed $TRANSACTION_COUNT transactions"
else
    echo "Processing failed: $(jq -r '.[0].Error' results.json)"
fi
```

### CSV Output for Spreadsheets
```bash
# Extract and get CSV summary
bankstmt extract --input *.pdf --output csv > extraction_results.csv

# Parse and get transaction CSV
bankstmt parse --input *.txt --bank CSOB --format csv > transactions.csv
```

### Table Output for Human Reading
```bash
# Default table format
bankstmt database status
# Output:
# Database Status:
#   Accounts: 3
#   Transactions: 1,250
#   Statements: 45
```

## Integration Examples

### 1. CI/CD Pipeline
```yaml
# .github/workflows/process-statements.yml
name: Process Bank Statements

on:
  schedule:
    - cron: '0 2 * * *'  # Daily at 2 AM

jobs:
  process:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
          
      - name: Process Statements
        run: |
          # Process all PDFs and capture results
          bankstmt process \
            --input ./statements/*.pdf \
            --bank CSOB \
            --output json > results.json
          
          # Check for failures
          FAILED_COUNT=$(jq '[.[] | select(.Success == false)] | length' results.json)
          
          if [ "$FAILED_COUNT" -gt 0 ]; then
            echo "❌ $FAILED_COUNT files failed processing"
            jq '[.[] | select(.Success == false)]' results.json
            exit 1
          else
            echo "✅ All files processed successfully"
          fi
          
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: processing-results
          path: results.json
```

### 2. Docker Container
```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app

COPY BankStatementParsing.CLI/bin/Release/net9.0/ .
COPY scripts/ ./scripts/

# Make scripts executable
RUN chmod +x scripts/*.sh

# Set up volumes for data
VOLUME ["/data/input", "/data/output", "/data/database"]

# Default command
ENTRYPOINT ["dotnet", "BankStatementParsing.CLI.dll"]
CMD ["--help"]
```

```bash
# Build and run
docker build -t bankstmt-cli .

# Process files in container
docker run -v ./statements:/data/input \
           -v ./output:/data/output \
           bankstmt-cli process --input /data/input/*.pdf --bank CSOB
```

### 3. Monitoring and Alerting
```bash
#!/bin/bash
# monitoring.sh - Check processing health

# Get database status as JSON
STATUS=$(bankstmt database status --output json)

# Extract metrics
TRANSACTION_COUNT=$(echo "$STATUS" | jq '.TransactionCount')
LAST_MODIFIED=$(echo "$STATUS" | jq -r '.LastModified')

# Check if processing is recent
LAST_TIMESTAMP=$(date -d "$LAST_MODIFIED" +%s)
CURRENT_TIMESTAMP=$(date +%s)
HOURS_SINCE=$((($CURRENT_TIMESTAMP - $LAST_TIMESTAMP) / 3600))

# Alert if no recent activity
if [ $HOURS_SINCE -gt 24 ]; then
    echo "⚠️  WARNING: No database activity in $HOURS_SINCE hours"
    # Send alert (Slack, email, etc.)
fi

echo "📊 Current status: $TRANSACTION_COUNT transactions, last activity ${HOURS_SINCE}h ago"
```

### 4. Web Hook Integration
```bash
#!/bin/bash
# webhook-processor.sh - Process files triggered by webhook

# Webhook payload handling
WEBHOOK_DATA=$(cat)
FILE_PATH=$(echo "$WEBHOOK_DATA" | jq -r '.file_path')
BANK_TYPE=$(echo "$WEBHOOK_DATA" | jq -r '.bank_type // "CSOB"')

# Validate file exists
if [ ! -f "$FILE_PATH" ]; then
    echo "❌ File not found: $FILE_PATH"
    exit 1
fi

# Process the file
RESULT=$(bankstmt process \
    --input "$FILE_PATH" \
    --bank "$BANK_TYPE" \
    --output json)

# Send result back via webhook
curl -X POST "$RESULT_WEBHOOK_URL" \
     -H "Content-Type: application/json" \
     -d "$RESULT"
```

## Advanced Patterns

### 1. Error Recovery
```bash
#!/bin/bash
# retry-failed.sh - Retry failed processing

FAILED_DIR="./failed"
MAX_RETRIES=3

for failed_file in "$FAILED_DIR"/*.pdf; do
    [ ! -f "$failed_file" ] && continue
    
    filename=$(basename "$failed_file")
    retry_count=0
    
    while [ $retry_count -lt $MAX_RETRIES ]; do
        echo "Retry $((retry_count + 1))/$MAX_RETRIES for $filename"
        
        if bankstmt process --input "$failed_file" --bank CSOB --quiet; then
            echo "✅ Successfully processed $filename on retry"
            rm "$failed_file"  # Remove from failed directory
            break
        else
            retry_count=$((retry_count + 1))
            sleep $((retry_count * 5))  # Exponential backoff
        fi
    done
    
    if [ $retry_count -eq $MAX_RETRIES ]; then
        echo "❌ Failed to process $filename after $MAX_RETRIES retries"
    fi
done
```

### 2. Multi-Bank Processing
```bash
#!/bin/bash
# multi-bank.sh - Process files from different banks

declare -A BANK_DIRS=(
    ["./csob"]="CSOB"
    ["./kb"]="KB"
    ["./unicredit"]="UNICREDIT"
)

for dir in "${!BANK_DIRS[@]}"; do
    bank_type="${BANK_DIRS[$dir]}"
    
    if [ -d "$dir" ]; then
        echo "Processing $bank_type files from $dir"
        
        bankstmt process \
            --input "$dir"/*.pdf \
            --bank "$bank_type" \
            --account "$bank_type" \
            --verbose
    fi
done
```

### 3. Data Validation Pipeline
```bash
#!/bin/bash
# validation.sh - Validate processed data

# Process and validate
RESULTS=$(bankstmt process --input statement.pdf --bank CSOB --output json)

# Extract validation metrics
TRANSACTION_COUNT=$(echo "$RESULTS" | jq '.[0].TransactionCount')
OPENING_BALANCE=$(echo "$RESULTS" | jq '.[0].OpeningBalance')
CLOSING_BALANCE=$(echo "$RESULTS" | jq '.[0].ClosingBalance')

# Validate business rules
if [ "$TRANSACTION_COUNT" -eq 0 ]; then
    echo "⚠️  Warning: No transactions found"
fi

if [ "$OPENING_BALANCE" = "null" ] || [ "$CLOSING_BALANCE" = "null" ]; then
    echo "⚠️  Warning: Missing balance information"
fi

# Export for external validation
echo "$RESULTS" | jq '.[0]' > validation_data.json
```

## Troubleshooting Commands

### Debug Processing Issues
```bash
# Enable verbose logging
bankstmt process --input statement.pdf --bank CSOB --verbose --log-level Debug

# Test individual steps
bankstmt extract --input statement.pdf --verbose
bankstmt parse --input statement.txt --bank CSOB --verbose
bankstmt import --input statement.json --dry-run --verbose

# Check database status
bankstmt database status --verbose
```

### Performance Analysis
```bash
# Time each step
time bankstmt extract --input large_statement.pdf
time bankstmt parse --input large_statement.txt --bank CSOB
time bankstmt import --input large_statement.json

# Memory usage monitoring
/usr/bin/time -v bankstmt process --input statement.pdf --bank CSOB
```

### Data Recovery
```bash
# Backup database before operations
cp Database/bankstatements.db Database/bankstatements.db.backup

# Clear problematic data
bankstmt database clear --confirmed

# Re-import from JSON files
find ./processed -name "*.json" -exec bankstmt import --input {} \;
```