namespace CodeForge.Core.Execution;

public sealed class ExecutionRecord
{
    public required Guid Id { get; init; }
    public required ExecutionRequest Request { get; init; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public int? ExitCode { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
