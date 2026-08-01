using CodeForge.Api.Hubs;
using CodeForge.Core.Execution;
using Microsoft.AspNetCore.SignalR;

namespace CodeForge.Api.Execution;

public sealed class SignalRExecutionEventPublisher(IHubContext<ExecutionHub> hub) : IExecutionEventPublisher
{
    public Task PublishStatusAsync(Guid executionId, ExecutionStatus status) =>
        Group(executionId).SendAsync("status", new { executionId, status });

    public Task PublishOutputAsync(Guid executionId, string stream, string chunk) =>
        Group(executionId).SendAsync("output", new { executionId, stream, chunk });

    public Task PublishCompletedAsync(Guid executionId, ExecutionStatus status,
        string? stdout, string? stderr, int? exitCode, double? durationMs) =>
        Group(executionId).SendAsync("completed",
            new { executionId, status, stdout, stderr, exitCode, durationMs });

    private IClientProxy Group(Guid executionId) =>
        hub.Clients.Group(executionId.ToString());
}
