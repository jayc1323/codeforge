namespace CodeForge.Core.Execution;

public interface IExecutionQueue
{
    ValueTask EnqueueAsync(ExecutionRecord record, CancellationToken cancellationToken);
    ValueTask<ExecutionRecord> DequeueAsync(CancellationToken cancellationToken);
}
