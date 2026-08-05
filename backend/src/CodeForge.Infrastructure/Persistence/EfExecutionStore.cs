using CodeForge.Core.Execution;
using Microsoft.EntityFrameworkCore;

namespace CodeForge.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed execution store. Uses a DbContextFactory so the singleton
/// ExecutionWorker can safely persist results without a request scope.
/// </summary>
public sealed class EfExecutionStore(IDbContextFactory<CodeForgeDbContext> contextFactory) : IExecutionStore
{
    public async Task AddAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Executions.Add(ToEntity(record));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExecutionRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Executions.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpdateAsync(ExecutionRecord record, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Executions.FirstOrDefaultAsync(e => e.Id == record.Id, cancellationToken);
        if (entity is null)
            return;

        entity.Status = record.Status;
        entity.Stdout = record.Stdout;
        entity.Stderr = record.Stderr;
        entity.ExitCode = record.ExitCode;
        entity.DurationMs = record.Duration?.TotalMilliseconds;
        entity.CompletedAt = record.CompletedAt;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ExecutionEntity ToEntity(ExecutionRecord record) => new()
    {
        Id = record.Id,
        UserId = record.UserId,
        Language = record.Request.Language,
        SourceCode = record.Request.SourceCode,
        StandardInput = record.Request.StandardInput,
        Status = record.Status,
        Stdout = record.Stdout,
        Stderr = record.Stderr,
        ExitCode = record.ExitCode,
        DurationMs = record.Duration?.TotalMilliseconds,
        CreatedAt = record.CreatedAt,
        CompletedAt = record.CompletedAt
    };

    private static ExecutionRecord ToRecord(ExecutionEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        Request = new ExecutionRequest(entity.Language, entity.SourceCode, entity.StandardInput),
        Status = entity.Status,
        Stdout = entity.Stdout,
        Stderr = entity.Stderr,
        ExitCode = entity.ExitCode,
        Duration = entity.DurationMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null,
        CreatedAt = entity.CreatedAt,
        CompletedAt = entity.CompletedAt
    };
}
