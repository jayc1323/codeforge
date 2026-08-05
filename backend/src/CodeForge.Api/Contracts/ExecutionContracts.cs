using System.ComponentModel.DataAnnotations;
using CodeForge.Core.Execution;

namespace CodeForge.Api.Contracts;

public sealed record SubmitExecutionRequest(
    [Required] string Language,
    [Required] string SourceCode,
    string? StandardInput);

public sealed record SubmitExecutionResponse(Guid Id, ExecutionStatus Status);

public sealed record ExecutionResponse(
    Guid Id,
    string Language,
    ExecutionStatus Status,
    string? SourceCode,
    string? StandardInput,
    string? Stdout,
    string? Stderr,
    int? ExitCode,
    double? DurationMs,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static ExecutionResponse FromRecord(ExecutionRecord record) => new(
        record.Id,
        record.Request.Language,
        record.Status,
        record.Request.SourceCode,
        record.Request.StandardInput,
        record.Stdout,
        record.Stderr,
        record.ExitCode,
        record.Duration?.TotalMilliseconds,
        record.CreatedAt,
        record.CompletedAt);
}

public sealed record LanguageResponse(string Id, string DisplayName, string Version, string DocsUrl);
