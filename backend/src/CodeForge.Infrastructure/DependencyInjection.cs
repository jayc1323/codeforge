using CodeForge.Core.Execution;
using CodeForge.Core.Languages;
using CodeForge.Infrastructure.Execution;
using CodeForge.Infrastructure.Languages;
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
        services.AddSingleton<IExecutionStore, InMemoryExecutionStore>();
        services.AddSingleton<ILanguageInfoService, LanguageInfoService>();

        if (string.Equals(configuration["Execution:Runner"], "docker", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IExecutionRunner, DockerRunner>();
        else
            services.AddSingleton<IExecutionRunner, LocalProcessRunner>();

        services.AddHostedService<ExecutionWorker>();
        return services;
    }
}
