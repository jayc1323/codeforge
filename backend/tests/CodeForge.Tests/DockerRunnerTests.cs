using System.Diagnostics;
using CodeForge.Core.Execution;
using CodeForge.Infrastructure.Execution;
using Microsoft.Extensions.Options;

namespace CodeForge.Tests;

public class DockerRunnerTests
{
    private readonly DockerRunner _runner = new(Options.Create(new DockerRunnerOptions()));

    public DockerRunnerTests()
    {
        if (!DockerAvailable())
            return; // docker not installed in this environment; tests become no-ops
    }

    private bool DockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            })!;
            process.WaitForExit(10_000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Python_RunsInContainer_WithStdin()
    {
        if (!DockerAvailable()) return;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "name = input()\nprint(f'hi {name}')", "docker"),
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("hi docker\n", outcome.Stdout);
    }

    [Fact]
    public async Task Container_HasNoNetworkAccess()
    {
        if (!DockerAvailable()) return;

        const string source = """
            import socket, sys
            try:
                socket.create_connection(("8.8.8.8", 53), timeout=3)
                print("NETWORK OPEN")
                sys.exit(1)
            except OSError:
                print("network blocked")
            """;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", source), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("network blocked\n", outcome.Stdout);
    }

    [Fact]
    public async Task Container_CannotSeeHostFilesystem()
    {
        if (!DockerAvailable()) return;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "import os\nprint(os.path.exists('/root/codeforge'))"),
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("False\n", outcome.Stdout);
    }

    [Fact]
    public async Task Container_EnforcesMemoryLimit()
    {
        if (!DockerAvailable()) return;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "x = bytearray(1024 * 1024 * 1024)\nprint('allocated')"),
            CancellationToken.None);

        // OOM-killed by the container's 256MB cgroup limit before it can print.
        Assert.Equal(ExecutionStatus.Failed, outcome.Status);
        Assert.DoesNotContain("allocated", outcome.Stdout);
    }

    [Fact]
    public async Task Container_InfiniteLoop_TimesOutAndIsRemoved()
    {
        if (!DockerAvailable()) return;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "while True: pass"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.TimedOut, outcome.Status);
    }

    [Fact]
    public async Task Cpp_CompilesAndRunsInContainer()
    {
        if (!DockerAvailable()) return;

        const string source = "#include <iostream>\nint main(){ int n; std::cin >> n; std::cout << n*n << std::endl; }";

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("cpp", source, "9"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("81\n", outcome.Stdout);
    }

    [Fact]
    public async Task Cpp_SyntaxError_ReportsCompileErrorWithCompilerOutput()
    {
        if (!DockerAvailable()) return;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("cpp", "int main( { not valid c++ }"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.CompileError, outcome.Status);
        Assert.Contains("error", outcome.Stderr);
    }

    [Fact]
    public async Task ProgramExitingWithTimeoutExitCode_IsNotMisreported()
    {
        if (!DockerAvailable()) return;

        // Exit code 124 collides with GNU timeout's sentinel; marker files must disambiguate.
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("cpp", "int main(){ return 124; }"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Failed, outcome.Status);
        Assert.Equal(124, outcome.ExitCode);
    }

    [Fact]
    public async Task TypeScript_RunsInContainer()
    {
        if (!DockerAvailable()) return;

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("typescript", "console.log([2, 3].map(x => x ** 2).join(','))"),
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("4,9\n", outcome.Stdout);
    }
}
