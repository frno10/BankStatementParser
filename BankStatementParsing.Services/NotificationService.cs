using BankStatementParsing.Core.Models;
using BankStatementParsing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace BankStatementParsing.Services;

public interface INotificationService
{
    Task SendProcessingCompleteNotificationAsync(int userId, string fileName, int transactionCount);
    Task SendProcessingFailedNotificationAsync(int userId, string fileName, string error);
    Task SendLargeTransactionNotificationAsync(int userId, Transaction transaction);
    Task<NotificationSettings> GetNotificationSettingsAsync(int userId);
    Task UpdateNotificationSettingsAsync(NotificationSettings settings);
}

public class NotificationService : INotificationService
{
    private readonly BankStatementParsingContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        BankStatementParsingContext context,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendProcessingCompleteNotificationAsync(int userId, string fileName, int transactionCount)
    {
        var settings = await GetNotificationSettingsAsync(userId);
        if (!settings.NotifyOnProcessingComplete) return;

        var message = $"Bank statement processing completed successfully!\n\n" +
                     $"File: {fileName}\n" +
                     $"Transactions processed: {transactionCount}\n" +
                     $"Processed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

        await SendNotificationAsync(settings, "Processing Complete", message);
    }

    public async Task SendProcessingFailedNotificationAsync(int userId, string fileName, string error)
    {
        var settings = await GetNotificationSettingsAsync(userId);
        if (!settings.NotifyOnProcessingFailed) return;

        var message = $"Bank statement processing failed!\n\n" +
                     $"File: {fileName}\n" +
                     $"Error: {error}\n" +
                     $"Failed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

        await SendNotificationAsync(settings, "Processing Failed", message);
    }

    public async Task SendLargeTransactionNotificationAsync(int userId, Transaction transaction)
    {
        var settings = await GetNotificationSettingsAsync(userId);
        if (!settings.NotifyOnLargeTransactions || 
            Math.Abs(transaction.Amount) < (double)settings.LargeTransactionThreshold) return;

        var message = $"Large transaction detected!\n\n" +
                     $"Amount: {transaction.Amount:C}\n" +
                     $"Date: {transaction.Date:yyyy-MM-dd}\n" +
                     $"Description: {transaction.Description}\n" +
                     $"Merchant: {transaction.Merchant?.Name ?? "Unknown"}";

        await SendNotificationAsync(settings, "Large Transaction Alert", message);
    }

    public async Task<NotificationSettings> GetNotificationSettingsAsync(int userId)
    {
        var settings = await _context.NotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            settings = new NotificationSettings { UserId = userId };
            _context.NotificationSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task UpdateNotificationSettingsAsync(NotificationSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _context.NotificationSettings.Update(settings);
        await _context.SaveChangesAsync();
    }

    private async Task SendNotificationAsync(NotificationSettings settings, string subject, string message)
    {
        var tasks = new List<Task>();

        if (settings.EmailEnabled && !string.IsNullOrEmpty(settings.EmailAddress))
        {
            tasks.Add(SendEmailAsync(settings.EmailAddress, subject, message));
        }

        if (settings.SmsEnabled && !string.IsNullOrEmpty(settings.PhoneNumber))
        {
            tasks.Add(SendSmsAsync(settings.PhoneNumber, message));
        }

        await Task.WhenAll(tasks);
    }

    private async Task SendEmailAsync(string email, string subject, string message)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUser = _configuration["Email:Username"];
            var smtpPass = _configuration["Email:Password"];
            var fromEmail = _configuration["Email:FromAddress"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUser))
            {
                _logger.LogWarning("Email configuration not found, skipping email notification");
                return;
            }

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUser, "Bank Statement Parser"),
                Subject = subject,
                Body = message,
                IsBodyHtml = false
            };

            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email notification sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification to {Email}", email);
        }
    }

    private async Task SendSmsAsync(string phoneNumber, string message)
    {
        try
        {
            // Placeholder for SMS implementation
            // You would integrate with services like Twilio, AWS SNS, etc.
            _logger.LogInformation("SMS notification would be sent to {PhoneNumber}: {Message}", phoneNumber, message);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS notification to {PhoneNumber}", phoneNumber);
        }
    }
}