using Microsoft.AspNetCore.Identity;

namespace CodeForge.Infrastructure.Persistence;

public sealed class ApplicationUser : IdentityUser
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
