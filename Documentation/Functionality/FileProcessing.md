# File Processing Functionality

## Overview
The file processing system enables automatic bank statement parsing through a file-based approach where users drop PDF files into designated account folders.

## Folder Structure

```
AccountData/
├── Account1/
│   ├── Inbox/          # Drop PDF files here
│   ├── Processing/     # Files currently being processed
│   ├── Processed/      # Successfully processed files
│   └── Failed/         # Failed files with error logs
├── Account2/
│   ├── Inbox/
│   ├── Processing/
│   ├── Processed/
│   └── Failed/
└── Account3/
    ├── Inbox/
    ├── Processing/
    ├── Processed/
    └── Failed/
```

## Processing Workflow

### 1. File Detection
- **FileWatcher Service** monitors all `Inbox` folders
- Detects new PDF files dropped into account folders
- Validates file format and size before processing

### 2. File Processing States
1. **Inbox**: Initial state when file is dropped
2. **Processing**: File moved here during active processing
3. **Processed**: Successfully parsed and data stored in database
4. **Failed**: Processing failed with detailed error logs

### 3. Processing Steps
1. Move file from `Inbox` to `Processing`
2. Extract text from PDF using optimized parsers
3. Parse bank-specific transaction data
4. Validate and clean extracted data
5. Store transactions in SQLite database
6. Move file to `Processed` or `Failed` based on outcome

## Configuration

### Account Configuration
```json
{
  "AccountProcessing": {
    "Accounts": {
      "Account1": {
        "Name": "Personal Checking",
        "BankType": "Chase",
        "AutoDetectBank": true,
        "ProcessingRules": {
          "RequireBalanceValidation": true,
          "AllowDuplicates": false,
          "CategoryMappings": {
            "AMAZON": "Shopping",
            "GROCERY": "Food"
          }
        }
      },
      "Account2": {
        "Name": "Business Account",
        "BankType": "BankOfAmerica",
        "AutoDetectBank": false
      }
    }
  }
}
```

### File Processing Rules
- **Supported Formats**: PDF only
- **Max File Size**: 10MB
- **Naming Convention**: Optional but recommended: `YYYY-MM_BankName_AccountLast4.pdf`
- **Duplicate Handling**: Files with same name are versioned

## Error Handling

### Common Errors
1. **Unsupported PDF Format**: Non-text PDF or encrypted
2. **Parse Errors**: Unrecognized bank format or corrupted data
3. **Validation Errors**: Missing required fields or invalid data
4. **Database Errors**: Connection issues or constraint violations

### Error Recovery
- Failed files remain in `Failed` folder with detailed error logs
- Manual reprocessing available through API
- Retry logic with exponential backoff for transient errors

## Performance Features

### Concurrent Processing
- Multiple files processed simultaneously
- Account-level processing isolation
- Configurable concurrency limits per account

### Memory Optimization
- Streaming PDF processing for large files
- Immediate data persistence to prevent memory leaks
- Configurable batch sizes for transaction processing

### Monitoring
- Real-time processing status via API
- File processing metrics and statistics
- Performance monitoring and alerting

## Integration Points

### Database Integration
- Automatic SQLite database creation if not exists
- Transaction-safe processing with rollback on errors
- Optimized indexes for fast querying

### Logging Integration
- Comprehensive logging of all processing steps
- Separate log files per processing session
- Structured logging for easy analysis

### API Integration
- REST endpoints for processing status
- Manual processing triggers
- File upload via API as alternative to file drop

## Security Considerations

### File Security
- Virus scanning before processing
- File type validation and content inspection
- Secure file handling and cleanup

### Data Security
- Sensitive data encryption in database
- Secure file storage and access controls
- Audit trail for all file operations

## Extensibility

### Adding New Banks
1. Create bank-specific parser class
2. Register parser in dependency injection
3. Add bank configuration to settings
4. Test with sample statements

### Custom Processing Rules
- Plugin-based architecture for custom processors
- Configurable validation rules per account
- Custom categorization and tagging rules 