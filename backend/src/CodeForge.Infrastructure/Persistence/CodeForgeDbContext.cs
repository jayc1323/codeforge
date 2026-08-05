using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CodeForge.Infrastructure.Persistence;

public sealed class CodeForgeDbContext(DbContextOptions<CodeForgeDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ExecutionEntity> Executions => Set<ExecutionEntity>();
    public DbSet<SnippetEntity> Snippets => Set<SnippetEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ExecutionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Language).HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });

        builder.Entity<SnippetEntity>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Title).HasMaxLength(200);
            entity.Property(s => s.Language).HasMaxLength(32);
            entity.HasIndex(s => new { s.UserId, s.UpdatedAt });
        });
    }
}
