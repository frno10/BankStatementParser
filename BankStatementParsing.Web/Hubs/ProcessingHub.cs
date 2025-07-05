using Microsoft.AspNetCore.SignalR;

namespace BankStatementParsing.Web.Hubs;

public class ProcessingHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Handle disconnection if needed
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IProcessingHubClient
{
    Task ProcessingStarted(string fileName);
    Task ProcessingProgress(string fileName, int progress, string stage);
    Task ProcessingCompleted(string fileName, int transactionCount, bool success);
    Task ProcessingFailed(string fileName, string error);
    Task TransactionAdded(object transaction);
    Task MetricsUpdated(object metrics);
}