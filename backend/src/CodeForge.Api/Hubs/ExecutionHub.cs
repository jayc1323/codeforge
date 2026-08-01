using Microsoft.AspNetCore.SignalR;

namespace CodeForge.Api.Hubs;

public class ExecutionHub : Hub
{
    public Task WatchExecution(string executionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, executionId);

    public Task UnwatchExecution(string executionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, executionId);
}
