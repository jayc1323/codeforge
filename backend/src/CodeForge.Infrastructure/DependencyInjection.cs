using CodeForge.Core.Execution;
using CodeForge.Core.Languages;
using CodeForge.Infrastructure.Execution;
using CodeForge.Infrastructure.Languages;
using CodeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCodeForgeInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DockerRunnerOptions>(
            configuration.GetSection(DockerRunnerOptions.SectionName));

        services.AddSingleton<IExecutionQueue, ExecutionQueue>();
        services.AddSingleton<ILanguageInfoService, LanguageInfoService>();

        var connectionString = configuration.GetConnectionString("CodeForge");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContextFactory<CodeForgeDbContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddSingleton<IExecutionStore, EfExecutionStore>();

            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.Password.RequireDigit = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredLength = 8;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<CodeForgeDbContext>();
        }
        else
        {
            services.AddSingleton<IExecutionStore, InMemoryExecutionStore>();
        }

        if (string.Equals(configuration["Execution:Runner"], "docker", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IExecutionRunner, DockerRunner>();
        else
            services.AddSingleton<IExecutionRunner, LocalProcessRunner>();

        services.AddHostedService<ExecutionWorker>();
        return services;
    }
}
