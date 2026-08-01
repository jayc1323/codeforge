namespace CodeForge.Core.Execution;

public interface IExecutionEventPublisher
{
    Task PublishStatusAsync(Guid executionId, ExecutionStatus status);
    Task PublishOutputAsync(Guid executionId, string stream, string chunk);
    Task PublishCompletedAsync(Guid executionId, ExecutionStatus status,
        string? stdout, string? stderr, int? exitCode, double? durationMs);
}
