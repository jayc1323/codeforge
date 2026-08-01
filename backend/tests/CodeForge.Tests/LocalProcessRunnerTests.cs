using CodeForge.Core.Execution;
using CodeForge.Infrastructure.Execution;

namespace CodeForge.Tests;

public class LocalProcessRunnerTests
{
    private readonly LocalProcessRunner _runner = new();

    [Fact]
    public async Task Python_CapturesStdout()
    {
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "print('hello')\nprint(2 ** 10)"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("hello\n1024\n", outcome.Stdout);
        Assert.Equal(0, outcome.ExitCode);
    }

    [Fact]
    public async Task Python_PipesStandardInput()
    {
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "name = input()\nprint(f'hi {name}')", "world"),
            CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("hi world\n", outcome.Stdout);
    }

    [Fact]
    public async Task Python_InfiniteLoop_TimesOut()
    {
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "while True: pass"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.TimedOut, outcome.Status);
        Assert.Null(outcome.ExitCode);
    }

    [Fact]
    public async Task Python_RuntimeError_ReportsFailed()
    {
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "raise ValueError('boom')"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Failed, outcome.Status);
        Assert.Contains("ValueError: boom", outcome.Stderr);
        Assert.NotEqual(0, outcome.ExitCode);
    }

    [Fact]
    public async Task Cpp_CompilesAndRuns()
    {
        const string source = "#include <iostream>\nint main(){ int n; std::cin >> n; std::cout << n*n << std::endl; }";

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("cpp", source, "7"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("49\n", outcome.Stdout);
    }

    [Fact]
    public async Task Cpp_SyntaxError_ReportsCompileError()
    {
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("cpp", "int main( { not valid c++ }"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.CompileError, outcome.Status);
        Assert.Contains("error", outcome.Stderr);
    }

    [Fact]
    public async Task Output_IsCapped()
    {
        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "print('x' * 10_000_000)"), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.True(outcome.Stdout.Length <= 64 * 1024);
    }

    [Fact]
    public async Task TypeScript_RunsViaTsx()
    {
        const string source = "const nums: number[] = [1, 2, 3];\nconsole.log(nums.map(x => x * x).join(','));";

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("typescript", source), CancellationToken.None);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.Equal("1,4,9\n", outcome.Stdout);
    }

    [Fact]
    public async Task Python_StreamsOutputChunksViaProgress()
    {
        var chunks = new List<(string Stream, string Chunk)>();
        var progress = new CollectingProgress(chunks);

        var outcome = await _runner.RunAsync(
            new ExecutionRequest("python", "print('a')\nprint('b')"),
            CancellationToken.None, progress);

        Assert.Equal(ExecutionStatus.Completed, outcome.Status);
        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("stdout", c.Stream));
        Assert.Equal("a\nb\n", string.Concat(chunks.Select(c => c.Chunk)));
    }

    private sealed class CollectingProgress(List<(string, string)> chunks) : IExecutionProgress
    {
        public Task OutputAsync(string stream, string chunk)
        {
            chunks.Add((stream, chunk));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task UnsupportedLanguage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _runner.RunAsync(new ExecutionRequest("cobol", "DISPLAY 'HI'."), CancellationToken.None));
    }
}
