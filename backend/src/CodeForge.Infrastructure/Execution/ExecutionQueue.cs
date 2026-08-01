using System.Threading.Channels;
using CodeForge.Core.Execution;

namespace CodeForge.Infrastructure.Execution;

public sealed class ExecutionQueue : IExecutionQueue
{
    private readonly Channel<ExecutionRecord> _channel = Channel.CreateUnbounded<ExecutionRecord>();

    public ValueTask EnqueueAsync(ExecutionRecord record, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(record, cancellationToken);

    public ValueTask<ExecutionRecord> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
