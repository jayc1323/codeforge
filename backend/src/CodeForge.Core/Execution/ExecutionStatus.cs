namespace CodeForge.Core.Execution;

public enum ExecutionStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    TimedOut,
    CompileError
}
