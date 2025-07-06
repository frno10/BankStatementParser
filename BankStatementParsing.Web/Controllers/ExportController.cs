using BankStatementParsing.Core.Models;
using BankStatementParsing.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementParsing.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        IExportService exportService,
        ILogger<ExportController> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    [HttpPost("request")]
    public async Task<ActionResult<ExportRequest>> CreateExportRequest([FromBody] ExportRequest request)
    {
        try
        {
            var exportRequest = await _exportService.CreateExportRequestAsync(request);
            
            // Process the export in the background
            _ = Task.Run(async () => await _exportService.ProcessExportRequestAsync(exportRequest.Id));
            
            return CreatedAtAction(nameof(GetExportRequests), new { userId = request.UserId }, exportRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating export request");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{userId}/requests")]
    public async Task<ActionResult<List<ExportRequest>>> GetExportRequests(int userId)
    {
        try
        {
            var requests = await _exportService.GetExportRequestsAsync(userId);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export requests for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("download/{requestId}")]
    public async Task<ActionResult> DownloadExport(int requestId)
    {
        try
        {
            var fileData = await _exportService.GetExportFileAsync(requestId);
            var requests = await _exportService.GetExportRequestsAsync(1); // You'd get the actual user ID
            var request = requests.FirstOrDefault(r => r.Id == requestId);
            
            if (request == null)
                return NotFound();

            var fileName = $"export_{requestId}.{GetFileExtension(request.Format)}";
            var contentType = GetContentType(request.Format);
            
            return File(fileData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading export {RequestId}", requestId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("formats")]
    public ActionResult<List<string>> GetSupportedFormats()
    {
        return Ok(new[] { "csv", "excel", "qif", "ofx", "json" });
    }

    private string GetFileExtension(string format)
    {
        return format.ToLower() switch
        {
            "csv" => "csv",
            "excel" => "xlsx",
            "qif" => "qif",
            "ofx" => "ofx",
            "json" => "json",
            _ => "txt"
        };
    }

    private string GetContentType(string format)
    {
        return format.ToLower() switch
        {
            "csv" => "text/csv",
            "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "qif" => "application/x-qif",
            "ofx" => "application/x-ofx",
            "json" => "application/json",
            _ => "text/plain"
        };
    }
}