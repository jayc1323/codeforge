using CodeForge.Core.Execution;
using CodeForge.Core.Languages;

namespace CodeForge.Infrastructure.Execution;

public sealed class LocalProcessRunner : IExecutionRunner
{
    private static readonly TimeSpan DefaultCompileTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromSeconds(10);

    public async Task<RunOutcome> RunAsync(ExecutionRequest request, CancellationToken cancellationToken,
        IExecutionProgress? progress = null)
    {
        var language = LanguageRegistry.Find(request.Language)
            ?? throw new ArgumentException($"Unsupported language: {request.Language}");

        var workDir = Path.Combine(Path.GetTempPath(), "codeforge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, language.SourceFileName), request.SourceCode, cancellationToken);

            foreach (var (name, contents) in language.ExtraFiles ?? new Dictionary<string, string>())
                await File.WriteAllTextAsync(Path.Combine(workDir, name), contents, cancellationToken);

            if (language.Compile is not null)
            {
                var compile = await ProcessRunner.RunAsync(
                    ResolveFileName(language.Compile.FileName, workDir), language.Compile.Arguments,
                    workDir, standardInput: null,
                    language.CompileTimeout ?? DefaultCompileTimeout, cancellationToken);

                if (compile.TimedOut || compile.ExitCode != 0)
                {
                    return new RunOutcome(
                        compile.TimedOut ? ExecutionStatus.TimedOut : ExecutionStatus.CompileError,
                        compile.Stdout, compile.Stderr, compile.ExitCode, compile.Duration);
                }
            }

            var run = await ProcessRunner.RunAsync(
                ResolveFileName(language.Run.FileName, workDir), language.Run.Arguments,
                workDir, request.StandardInput,
                language.RunTimeout ?? DefaultRunTimeout, cancellationToken,
                progress: progress);

            var status = run.TimedOut
                ? ExecutionStatus.TimedOut
                : run.ExitCode == 0 ? ExecutionStatus.Completed : ExecutionStatus.Failed;

            return new RunOutcome(status, run.Stdout, run.Stderr, run.ExitCode, run.Duration);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static string ResolveFileName(string fileName, string workDir) =>
        fileName.Contains(Path.DirectorySeparatorChar) && !Path.IsPathRooted(fileName)
            ? Path.Combine(workDir, fileName)
            : fileName;
}
