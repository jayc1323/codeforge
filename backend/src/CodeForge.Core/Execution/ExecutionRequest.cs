namespace CodeForge.Core.Execution;

public sealed record ExecutionRequest(
    string Language,
    string SourceCode,
    string? StandardInput = null);
