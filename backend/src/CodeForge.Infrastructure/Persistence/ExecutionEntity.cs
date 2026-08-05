using CodeForge.Core.Execution;

namespace CodeForge.Infrastructure.Persistence;

public sealed class ExecutionEntity
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public required string Language { get; set; }
    public required string SourceCode { get; set; }
    public string? StandardInput { get; set; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public int? ExitCode { get; set; }
    public double? DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
