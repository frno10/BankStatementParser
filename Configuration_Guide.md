# Configuration Guide for Enhanced Features

## Quick Setup Guide

### 1. Email Configuration

Add the following to your `appsettings.json`:

```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "notifications@yourcompany.com"
  }
}
```

### 2. Database Migration

After adding the new models, you'll need to create and apply a migration:

```bash
# Navigate to the project root
dotnet ef migrations add Enhanced_Features -p BankStatementParsing.Infrastructure -s BankStatementParsing.Web

# Apply the migration
dotnet ef database update -p BankStatementParsing.Infrastructure -s BankStatementParsing.Web
```

### 3. Testing the New Features

#### Test SignalR Connection
1. Open the web application
2. Check the connection status indicator in the top-right corner
3. It should show "Connected" with a green dot

#### Test Transaction Rules
1. Navigate to Settings > Transaction Rules
2. Create a test rule:
   - Name: "Grocery Categorization"
   - Condition: Description contains "GROCERY"
   - Action: Assign Category "Food & Dining"
3. Apply rules to existing transactions

#### Test Export Functionality
1. Navigate to Settings > Export Data
2. Select a date range and format
3. Create export request
4. Download the generated file

#### Test Notifications
1. Navigate to Settings > Notifications
2. Configure your email settings
3. Use the "Test Email" button to verify setup

### 4. Running the Application

```bash
# Start the web application
dotnet run --project BankStatementParsing.Web

# Or start the CLI for batch processing
dotnet run --project BankStatementParsing.CLI -- process --input statement.pdf --bank CSOB
```

## Feature Configuration

### Notification Settings
- Configure per-user notification preferences
- Set thresholds for large transaction alerts
- Enable/disable different notification types

### Export Settings
- Configure default export formats
- Set retention policies for export files
- Configure background processing limits

### Scheduled Jobs
- Set up automated file processing schedules
- Configure data export schedules
- Set up cleanup job schedules

## Troubleshooting

### Common Issues

1. **SignalR Connection Failed**
   - Check that SignalR is properly configured in Program.cs
   - Verify the hub route is correctly mapped
   - Check browser console for JavaScript errors

2. **Email Notifications Not Working**
   - Verify SMTP settings are correct
   - Check firewall settings for SMTP ports
   - Ensure app passwords are used for Gmail

3. **Export Files Not Generated**
   - Check file permissions in the exports directory
   - Verify EPPlus package is properly installed
   - Check application logs for export errors

### Logging

The application uses Serilog for comprehensive logging. Check the logs directory for detailed error information:

```
logs/bankstatement-web-YYYY-MM-DD.log
```

## Next Steps

1. **Test all features thoroughly**
2. **Configure production settings**
3. **Set up monitoring and alerts**
4. **Train users on new functionality**
5. **Plan for additional enhancements**

Your enhanced bank statement parser is now ready for production use!