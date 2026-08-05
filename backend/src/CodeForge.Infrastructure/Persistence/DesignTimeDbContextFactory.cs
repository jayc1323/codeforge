using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeForge.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` tooling at design time (migrations). Reads the
/// connection string from the ConnectionStrings__CodeForge environment variable,
/// or falls back to the local .secrets file so migrations work in dev shells.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CodeForgeDbContext>
{
    public CodeForgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CodeForge")
            ?? ReadFromSecretsFile()
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__CodeForge or create .secrets/dbconnectionstring.txt");

        var options = new DbContextOptionsBuilder<CodeForgeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CodeForgeDbContext(options);
    }

    private static string? ReadFromSecretsFile()
    {
        // Walk up from the AppDomain base directory looking for the repo's .secrets folder.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".secrets", "dbconnectionstring.txt");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate).Trim();
            directory = directory.Parent;
        }
        return null;
    }
}
