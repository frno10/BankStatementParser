#!/bin/bash

# Bank Statement Processing Script
# Demonstrates CLI command chaining and batch processing

set -e  # Exit on any error

# Configuration
BANK_TYPE=${1:-CSOB}
INPUT_DIR=${2:-./AccountData}
WORK_DIR=${3:-./temp}
VERBOSE=${VERBOSE:-false}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [BANK_TYPE] [INPUT_DIR] [WORK_DIR]"
    echo ""
    echo "Process bank statement PDFs with granular CLI commands"
    echo ""
    echo "Arguments:"
    echo "  BANK_TYPE   Bank type for parsing (default: CSOB)"
    echo "  INPUT_DIR   Directory containing PDF files (default: ./AccountData)"
    echo "  WORK_DIR    Working directory for intermediate files (default: ./temp)"
    echo ""
    echo "Environment Variables:"
    echo "  VERBOSE     Enable verbose output (true/false, default: false)"
    echo ""
    echo "Examples:"
    echo "  $0                           # Use all defaults"
    echo "  $0 CSOB ./statements        # Custom bank and input dir"
    echo "  VERBOSE=true $0              # Enable verbose output"
    echo ""
}

# Parse command line arguments
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    show_usage
    exit 0
fi

# Validate inputs
if [[ ! -d "$INPUT_DIR" ]]; then
    log_error "Input directory does not exist: $INPUT_DIR"
    exit 1
fi

# Set verbose flag
VERBOSE_FLAG=""
if [[ "$VERBOSE" == "true" ]]; then
    VERBOSE_FLAG="--verbose"
fi

log_info "Starting batch processing..."
log_info "Bank Type: $BANK_TYPE"
log_info "Input Directory: $INPUT_DIR"
log_info "Working Directory: $WORK_DIR"

# Create working directory
mkdir -p "$WORK_DIR"

# Find all PDF files
PDF_FILES=($(find "$INPUT_DIR" -name "*.pdf" -type f))

if [[ ${#PDF_FILES[@]} -eq 0 ]]; then
    log_warn "No PDF files found in $INPUT_DIR"
    exit 0
fi

log_info "Found ${#PDF_FILES[@]} PDF files to process"

# Method 1: Full pipeline processing (recommended for most cases)
log_info "Method 1: Using full pipeline processing..."

SUCCESS_COUNT=0
FAILED_COUNT=0

for pdf_file in "${PDF_FILES[@]}"; do
    filename=$(basename "$pdf_file")
    log_info "Processing: $filename"
    
    if dotnet run --project BankStatementParsing.CLI -- \
        process \
        --input "$pdf_file" \
        --bank "$BANK_TYPE" \
        --work-dir "$WORK_DIR" \
        $VERBOSE_FLAG \
        --quiet; then
        
        SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
        log_info "✓ Successfully processed: $filename"
    else
        FAILED_COUNT=$((FAILED_COUNT + 1))
        log_error "✗ Failed to process: $filename"
    fi
done

log_info "Pipeline processing complete: $SUCCESS_COUNT successful, $FAILED_COUNT failed"

# Method 2: Granular step-by-step processing (for advanced control)
log_info ""
log_info "Method 2: Demonstrating granular step-by-step processing..."

# Step 1: Extract text from all PDFs
log_info "Step 1: Extracting text from PDFs..."
if dotnet run --project BankStatementParsing.CLI -- \
    extract \
    --input "$INPUT_DIR"/*.pdf \
    --output-dir "$WORK_DIR/extracted" \
    --force \
    $VERBOSE_FLAG; then
    log_info "✓ Text extraction completed"
else
    log_error "✗ Text extraction failed"
    exit 1
fi

# Step 2: Parse extracted text files
log_info "Step 2: Parsing extracted text files..."
if dotnet run --project BankStatementParsing.CLI -- \
    parse \
    --input "$WORK_DIR/extracted"/*.txt \
    --bank "$BANK_TYPE" \
    --output-dir "$WORK_DIR/parsed" \
    --format json \
    $VERBOSE_FLAG; then
    log_info "✓ Parsing completed"
else
    log_error "✗ Parsing failed"
    exit 1
fi

# Step 3: Import parsed data (with dry run first)
log_info "Step 3a: Dry run import to preview..."
dotnet run --project BankStatementParsing.CLI -- \
    import \
    --input "$WORK_DIR/parsed"/*.json \
    --dry-run \
    $VERBOSE_FLAG

log_info "Step 3b: Actual import to database..."
if dotnet run --project BankStatementParsing.CLI -- \
    import \
    --input "$WORK_DIR/parsed"/*.json \
    --skip-duplicates \
    $VERBOSE_FLAG; then
    log_info "✓ Import completed"
else
    log_error "✗ Import failed"
    exit 1
fi

# Method 3: JSON output for further processing
log_info ""
log_info "Method 3: Using JSON output for analysis..."

# Get processing results as JSON
RESULTS_FILE="$WORK_DIR/results.json"
dotnet run --project BankStatementParsing.CLI -- \
    process \
    --input "${PDF_FILES[0]}" \
    --bank "$BANK_TYPE" \
    --work-dir "$WORK_DIR" \
    --output json \
    --quiet > "$RESULTS_FILE"

# Analyze results (requires jq)
if command -v jq >/dev/null 2>&1; then
    log_info "Analysis of processing results:"
    
    TOTAL_TRANSACTIONS=$(jq '.[0].TransactionCount // 0' "$RESULTS_FILE")
    TOTAL_DURATION=$(jq -r '.[0].TotalDuration // "N/A"' "$RESULTS_FILE")
    
    echo "  Total transactions: $TOTAL_TRANSACTIONS"
    echo "  Total duration: $TOTAL_DURATION"
    
    # Extract step-by-step timings
    if jq -e '.[0].ExtractStep' "$RESULTS_FILE" >/dev/null; then
        EXTRACT_TIME=$(jq -r '.[0].ExtractStep.Duration' "$RESULTS_FILE")
        PARSE_TIME=$(jq -r '.[0].ParseStep.Duration' "$RESULTS_FILE")
        IMPORT_TIME=$(jq -r '.[0].ImportStep.Duration' "$RESULTS_FILE")
        
        echo "  Step timings:"
        echo "    Extract: $EXTRACT_TIME"
        echo "    Parse: $PARSE_TIME"
        echo "    Import: $IMPORT_TIME"
    fi
else
    log_warn "jq not available for JSON analysis"
fi

# Database status check
log_info ""
log_info "Final database status:"
dotnet run --project BankStatementParsing.CLI -- \
    database status

# Cleanup options
read -p "Do you want to cleanup intermediate files in $WORK_DIR? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    rm -rf "$WORK_DIR"
    log_info "✓ Intermediate files cleaned up"
else
    log_info "Intermediate files kept in: $WORK_DIR"
fi

log_info "Batch processing script completed successfully!"