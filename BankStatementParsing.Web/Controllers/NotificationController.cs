using BankStatementParsing.Core.Models;
using BankStatementParsing.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementParsing.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationService notificationService,
        ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet("settings/{userId}")]
    public async Task<ActionResult<NotificationSettings>> GetNotificationSettings(int userId)
    {
        try
        {
            var settings = await _notificationService.GetNotificationSettingsAsync(userId);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification settings for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("settings")]
    public async Task<ActionResult<NotificationSettings>> UpdateNotificationSettings([FromBody] NotificationSettings settings)
    {
        try
        {
            await _notificationService.UpdateNotificationSettingsAsync(settings);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification settings");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("test-email")]
    public async Task<ActionResult> TestEmail([FromBody] TestEmailRequest request)
    {
        try
        {
            await _notificationService.SendProcessingCompleteNotificationAsync(
                request.UserId, 
                "test-statement.pdf", 
                25);
            return Ok(new { message = "Test email sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email");
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class TestEmailRequest
{
    public int UserId { get; set; }
}