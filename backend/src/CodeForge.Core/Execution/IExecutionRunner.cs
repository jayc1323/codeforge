namespace CodeForge.Core.Execution;

public sealed record RunOutcome(
    ExecutionStatus Status,
    string Stdout,
    string Stderr,
    int? ExitCode,
    TimeSpan Duration);

public interface IExecutionRunner
{
    Task<RunOutcome> RunAsync(ExecutionRequest request, CancellationToken cancellationToken,
        IExecutionProgress? progress = null);
}
