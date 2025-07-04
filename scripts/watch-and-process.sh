#!/bin/bash

# Watch and Process Script
# Automatically processes new PDF files as they appear in specified directories

set -e

# Configuration
WATCH_DIR=${1:-./AccountData}
BANK_TYPE=${2:-CSOB}
POLL_INTERVAL=${3:-10}
PROCESSED_DIR="$WATCH_DIR/Processed"
FAILED_DIR="$WATCH_DIR/Failed"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log_info() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') [INFO] $1"
}

log_warn() {
    echo -e "$(date '+%Y-%m-%d %H:%M:%S') ${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "$(date '+%Y-%m-%d %H:%M:%S') ${RED}[ERROR]${NC} $1"
}

show_usage() {
    echo "Usage: $0 [WATCH_DIR] [BANK_TYPE] [POLL_INTERVAL]"
    echo ""
    echo "Watch directory for new PDF files and process them automatically"
    echo ""
    echo "Arguments:"
    echo "  WATCH_DIR      Directory to watch for new PDF files (default: ./AccountData)"
    echo "  BANK_TYPE      Bank type for parsing (default: CSOB)"
    echo "  POLL_INTERVAL  Polling interval in seconds (default: 10)"
    echo ""
    echo "The script will:"
    echo "  - Watch for new .pdf files in WATCH_DIR"
    echo "  - Process them using the CLI tool"
    echo "  - Move successful files to WATCH_DIR/Processed"
    echo "  - Move failed files to WATCH_DIR/Failed"
    echo ""
    echo "Press Ctrl+C to stop watching"
    echo ""
}

# Parse command line arguments
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    show_usage
    exit 0
fi

# Validate inputs
if [[ ! -d "$WATCH_DIR" ]]; then
    log_error "Watch directory does not exist: $WATCH_DIR"
    exit 1
fi

# Create necessary directories
mkdir -p "$PROCESSED_DIR"
mkdir -p "$FAILED_DIR"

# File to track processed files
PROCESSED_LIST="/tmp/bankstmt_processed_$(date +%s).txt"
touch "$PROCESSED_LIST"

# Cleanup function
cleanup() {
    log_info "Stopping file watcher..."
    rm -f "$PROCESSED_LIST"
    exit 0
}

# Set up signal handlers
trap cleanup SIGINT SIGTERM

log_info "Starting file watcher..."
log_info "Watch Directory: $WATCH_DIR"
log_info "Bank Type: $BANK_TYPE"
log_info "Poll Interval: ${POLL_INTERVAL}s"
log_info "Processed files will be moved to: $PROCESSED_DIR"
log_info "Failed files will be moved to: $FAILED_DIR"
log_info ""
log_info "Press Ctrl+C to stop watching"
log_info ""

# Main watching loop
while true; do
    # Find new PDF files (not in processed list)
    NEW_FILES=()
    while IFS= read -r -d '' file; do
        if ! grep -Fxq "$file" "$PROCESSED_LIST"; then
            NEW_FILES+=("$file")
        fi
    done < <(find "$WATCH_DIR" -maxdepth 1 -name "*.pdf" -type f -print0)
    
    # Process new files
    for pdf_file in "${NEW_FILES[@]}"; do
        filename=$(basename "$pdf_file")
        log_info "New file detected: $filename"
        
        # Add to processed list immediately to avoid double processing
        echo "$pdf_file" >> "$PROCESSED_LIST"
        
        # Create a temporary work directory for this file
        work_dir="/tmp/bankstmt_work_$(date +%s)_$$"
        mkdir -p "$work_dir"
        
        # Process the file
        if dotnet run --project BankStatementParsing.CLI -- \
            process \
            --input "$pdf_file" \
            --bank "$BANK_TYPE" \
            --work-dir "$work_dir" \
            --quiet; then
            
            # Success - move to processed directory
            processed_file="$PROCESSED_DIR/$filename"
            mv "$pdf_file" "$processed_file"
            log_info "✓ Successfully processed and moved: $filename -> Processed/"
            
            # Log processing result to database
            dotnet run --project BankStatementParsing.CLI -- \
                database status --quiet > /dev/null
                
        else
            # Failure - move to failed directory with timestamp
            timestamp=$(date +%Y%m%d_%H%M%S)
            failed_file="$FAILED_DIR/${timestamp}_$filename"
            mv "$pdf_file" "$failed_file"
            log_error "✗ Processing failed, moved: $filename -> Failed/${timestamp}_$filename"
            
            # Create error log
            echo "Processing failed at $(date)" > "$FAILED_DIR/${timestamp}_$filename.error.log"
            echo "Bank type: $BANK_TYPE" >> "$FAILED_DIR/${timestamp}_$filename.error.log"
            echo "Original file: $pdf_file" >> "$FAILED_DIR/${timestamp}_$filename.error.log"
        fi
        
        # Cleanup work directory
        rm -rf "$work_dir"
    done
    
    # Show periodic status if files were processed
    if [[ ${#NEW_FILES[@]} -gt 0 ]]; then
        log_info "Processed ${#NEW_FILES[@]} file(s). Continuing to watch..."
        
        # Show current database status
        echo "Current database status:"
        dotnet run --project BankStatementParsing.CLI -- database status --quiet
        echo ""
    fi
    
    # Wait before next check
    sleep "$POLL_INTERVAL"
done