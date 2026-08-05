namespace CodeForge.Core.Execution;

public interface IExecutionStore
{
    Task AddAsync(ExecutionRecord record, CancellationToken cancellationToken = default);
    Task<ExecutionRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExecutionRecord record, CancellationToken cancellationToken = default);
}
