using System.Collections.Concurrent;
using CodeForge.Core.Execution;

namespace CodeForge.Infrastructure.Execution;

public sealed class InMemoryExecutionStore : IExecutionStore
{
    private readonly ConcurrentDictionary<Guid, ExecutionRecord> _records = new();

    public Task AddAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<ExecutionRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_records.TryGetValue(id, out var record) ? record : null);

    public Task UpdateAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }
}
