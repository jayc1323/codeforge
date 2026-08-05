namespace CodeForge.Infrastructure.Persistence;

public sealed class SnippetEntity
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public required string Title { get; set; }
    public required string Language { get; set; }
    public required string SourceCode { get; set; }
    public string? StandardInput { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
