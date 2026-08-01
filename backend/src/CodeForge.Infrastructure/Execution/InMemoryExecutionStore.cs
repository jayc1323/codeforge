using System.Collections.Concurrent;
using CodeForge.Core.Execution;

namespace CodeForge.Infrastructure.Execution;

public sealed class InMemoryExecutionStore : IExecutionStore
{
    private readonly ConcurrentDictionary<Guid, ExecutionRecord> _records = new();

    public void Add(ExecutionRecord record) => _records[record.Id] = record;

    public ExecutionRecord? Get(Guid id) =>
        _records.TryGetValue(id, out var record) ? record : null;
}
