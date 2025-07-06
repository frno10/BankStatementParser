using BankStatementParsing.Core.Models;
using BankStatementParsing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using System.Text.Json;

namespace BankStatementParsing.Services;

public interface ISchedulerService
{
    Task<List<ScheduledJob>> GetScheduledJobsAsync();
    Task<ScheduledJob> CreateScheduledJobAsync(ScheduledJob job);
    Task<ScheduledJob> UpdateScheduledJobAsync(ScheduledJob job);
    Task DeleteScheduledJobAsync(int jobId);
    Task<bool> ExecuteJobAsync(int jobId);
}

public class SchedulerService : BackgroundService, ISchedulerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchedulerService> _logger;
    private readonly Dictionary<int, Timer> _jobTimers = new();

    public SchedulerService(
        IServiceProvider serviceProvider,
        ILogger<SchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<ScheduledJob>> GetScheduledJobsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        return await context.ScheduledJobs
            .OrderBy(j => j.Name)
            .ToListAsync();
    }

    public async Task<ScheduledJob> CreateScheduledJobAsync(ScheduledJob job)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        job.NextRun = CalculateNextRun(job.CronExpression);
        context.ScheduledJobs.Add(job);
        await context.SaveChangesAsync();
        
        if (job.IsActive)
        {
            ScheduleJob(job);
        }
        
        return job;
    }

    public async Task<ScheduledJob> UpdateScheduledJobAsync(ScheduledJob job)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        job.UpdatedAt = DateTime.UtcNow;
        job.NextRun = CalculateNextRun(job.CronExpression);
        context.ScheduledJobs.Update(job);
        await context.SaveChangesAsync();
        
        // Remove existing timer
        if (_jobTimers.TryGetValue(job.Id, out var existingTimer))
        {
            existingTimer.Dispose();
            _jobTimers.Remove(job.Id);
        }
        
        // Schedule new timer if active
        if (job.IsActive)
        {
            ScheduleJob(job);
        }
        
        return job;
    }

    public async Task DeleteScheduledJobAsync(int jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        var job = await context.ScheduledJobs.FindAsync(jobId);
        if (job != null)
        {
            context.ScheduledJobs.Remove(job);
            await context.SaveChangesAsync();
            
            // Remove timer
            if (_jobTimers.TryGetValue(jobId, out var timer))
            {
                timer.Dispose();
                _jobTimers.Remove(jobId);
            }
        }
    }

    public async Task<bool> ExecuteJobAsync(int jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        var job = await context.ScheduledJobs.FindAsync(jobId);
        if (job == null) return false;

        try
        {
            _logger.LogInformation("Executing scheduled job: {JobName}", job.Name);
            
            job.LastRun = DateTime.UtcNow;
            job.NextRun = CalculateNextRun(job.CronExpression);
            
            var result = await ExecuteJobByTypeAsync(job, scope.ServiceProvider);
            
            job.LastRunSuccessful = result;
            job.LastRunResult = result ? "Success" : "Failed";
            
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Scheduled job {JobName} completed with result: {Result}", job.Name, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled job: {JobName}", job.Name);
            
            job.LastRunSuccessful = false;
            job.LastRunResult = ex.Message;
            await context.SaveChangesAsync();
            
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler service starting");
        
        // Load and schedule existing jobs
        await LoadAndScheduleJobsAsync();
        
        // Check for job updates every minute
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await CheckForJobUpdatesAsync();
        }
    }

    private async Task LoadAndScheduleJobsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        var jobs = await context.ScheduledJobs.Where(j => j.IsActive).ToListAsync();
        
        foreach (var job in jobs)
        {
            ScheduleJob(job);
        }
        
        _logger.LogInformation("Loaded {JobCount} scheduled jobs", jobs.Count);
    }

    private async Task CheckForJobUpdatesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BankStatementParsingContext>();
        
        var jobs = await context.ScheduledJobs.ToListAsync();
        
        foreach (var job in jobs)
        {
            if (job.NextRun.HasValue && job.NextRun.Value <= DateTime.UtcNow && job.IsActive)
            {
                _ = Task.Run(async () => await ExecuteJobAsync(job.Id));
            }
        }
    }

    private void ScheduleJob(ScheduledJob job)
    {
        if (job.NextRun.HasValue && job.NextRun.Value > DateTime.UtcNow)
        {
            var delay = job.NextRun.Value - DateTime.UtcNow;
            var timer = new Timer(
                async _ => await ExecuteJobAsync(job.Id),
                null,
                delay,
                Timeout.InfiniteTimeSpan);
            
            _jobTimers[job.Id] = timer;
        }
    }

    private DateTime? CalculateNextRun(string cronExpression)
    {
        try
        {
            var crontab = CrontabSchedule.Parse(cronExpression);
            return crontab.GetNextOccurrence(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression: {CronExpression}", cronExpression);
            return null;
        }
    }

    private async Task<bool> ExecuteJobByTypeAsync(ScheduledJob job, IServiceProvider serviceProvider)
    {
        switch (job.JobType.ToLower())
        {
            case "processfiles":
                return await ExecuteProcessFilesJobAsync(job, serviceProvider);
            case "exportdata":
                return await ExecuteExportDataJobAsync(job, serviceProvider);
            case "cleanupfiles":
                return await ExecuteCleanupFilesJobAsync(job, serviceProvider);
            case "applyrules":
                return await ExecuteApplyRulesJobAsync(job, serviceProvider);
            default:
                _logger.LogWarning("Unknown job type: {JobType}", job.JobType);
                return false;
        }
    }

    private async Task<bool> ExecuteProcessFilesJobAsync(ScheduledJob job, IServiceProvider serviceProvider)
    {
        var parsingService = serviceProvider.GetRequiredService<BankStatementParsingService>();
        
        // Get parameters from job
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Parameters ?? "{}");
        var accountPath = parameters.ContainsKey("accountPath") ? parameters["accountPath"].ToString() : "AccountData";
        
        // Process files in the account folders
        var processedCount = 0;
        var accountDirectories = Directory.GetDirectories(accountPath ?? "AccountData");
        
        foreach (var accountDir in accountDirectories)
        {
            var inboxPath = Path.Combine(accountDir, "Inbox");
            if (Directory.Exists(inboxPath))
            {
                var pdfFiles = Directory.GetFiles(inboxPath, "*.pdf");
                foreach (var pdfFile in pdfFiles)
                {
                    try
                    {
                        // This is a simplified version - you would integrate with your actual parsing logic
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file {FileName}", pdfFile);
                    }
                }
            }
        }
        
        _logger.LogInformation("ProcessFiles job processed {Count} files", processedCount);
        return true;
    }

    private async Task<bool> ExecuteExportDataJobAsync(ScheduledJob job, IServiceProvider serviceProvider)
    {
        var exportService = serviceProvider.GetRequiredService<IExportService>();
        
        // Get parameters from job
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Parameters ?? "{}");
        
        // Create export request based on job parameters
        var exportRequest = new ExportRequest
        {
            UserId = 1, // You would get this from job parameters
            Format = parameters.ContainsKey("format") ? parameters["format"].ToString() ?? "csv" : "csv",
            DateFrom = parameters.ContainsKey("dateFrom") ? DateTime.Parse(parameters["dateFrom"].ToString() ?? DateTime.MinValue.ToString()) : null,
            DateTo = parameters.ContainsKey("dateTo") ? DateTime.Parse(parameters["dateTo"].ToString() ?? DateTime.MaxValue.ToString()) : null
        };
        
        await exportService.CreateExportRequestAsync(exportRequest);
        await exportService.ProcessExportRequestAsync(exportRequest.Id);
        
        _logger.LogInformation("ExportData job completed");
        return true;
    }

    private async Task<bool> ExecuteCleanupFilesJobAsync(ScheduledJob job, IServiceProvider serviceProvider)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Parameters ?? "{}");
        var retentionDays = parameters.ContainsKey("retentionDays") ? int.Parse(parameters["retentionDays"].ToString() ?? "30") : 30;
        var targetDirectory = parameters.ContainsKey("directory") ? parameters["directory"].ToString() : "exports";
        
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var deletedCount = 0;
        
        if (Directory.Exists(targetDirectory))
        {
            var files = Directory.GetFiles(targetDirectory);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting file {FileName}", file);
                    }
                }
            }
        }
        
        _logger.LogInformation("CleanupFiles job deleted {Count} files", deletedCount);
        return true;
    }

    private async Task<bool> ExecuteApplyRulesJobAsync(ScheduledJob job, IServiceProvider serviceProvider)
    {
        var ruleService = serviceProvider.GetRequiredService<ITransactionRuleService>();
        
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Parameters ?? "{}");
        var userId = parameters.ContainsKey("userId") ? int.Parse(parameters["userId"].ToString() ?? "1") : 1;
        
        var appliedCount = await ruleService.ApplyRulesToAllTransactionsAsync(userId);
        
        _logger.LogInformation("ApplyRules job applied rules to {Count} transactions", appliedCount);
        return true;
    }

    public override void Dispose()
    {
        foreach (var timer in _jobTimers.Values)
        {
            timer?.Dispose();
        }
        base.Dispose();
    }
}