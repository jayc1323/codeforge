namespace CodeForge.Infrastructure.Execution;

public sealed class DockerRunnerOptions
{
    public const string SectionName = "DockerRunner";

    public Dictionary<string, string> Images { get; set; } = new()
    {
        ["python"] = "python:3.12-slim",
        ["cpp"] = "gcc:13",
        ["csharp"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["fsharp"] = "mcr.microsoft.com/dotnet/sdk:8.0",
        ["typescript"] = "codeforge-typescript:latest"
    };

    public int MemoryMb { get; set; } = 256;
    public double CpuCount { get; set; } = 1.0;
    public int PidsLimit { get; set; } = 128;
}
