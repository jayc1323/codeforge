using System.Text;
using CodeForge.Core.Execution;
using CodeForge.Core.Languages;
using Microsoft.Extensions.Options;

namespace CodeForge.Infrastructure.Execution;

public sealed class DockerRunner(IOptions<DockerRunnerOptions> options) : IExecutionRunner
{
    private static readonly TimeSpan DefaultCompileTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SafetyBuffer = TimeSpan.FromSeconds(15);
    private const int MaxOutputChars = 64 * 1024;

    private const string CompileFailedMarker = "__cf_compile_failed";
    private const string CompileTimeoutMarker = "__cf_compile_timeout";
    private const string RunTimeoutMarker = "__cf_run_timeout";

    private readonly DockerRunnerOptions _options = options.Value;

    public async Task<RunOutcome> RunAsync(ExecutionRequest request, CancellationToken cancellationToken,
        IExecutionProgress? progress = null)
    {
        var language = LanguageRegistry.Find(request.Language)
            ?? throw new ArgumentException($"Unsupported language: {request.Language}");

        if (!_options.Images.TryGetValue(language.Id, out var image))
            throw new InvalidOperationException($"No container image configured for language: {language.Id}");

        var compileTimeout = language.CompileTimeout ?? DefaultCompileTimeout;
        var runTimeout = language.RunTimeout ?? DefaultRunTimeout;

        var workDir = Path.Combine(Path.GetTempPath(), "codeforge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(workDir, language.SourceFileName), request.SourceCode, cancellationToken);

            foreach (var (name, contents) in language.ExtraFiles ?? new Dictionary<string, string>())
                await File.WriteAllTextAsync(Path.Combine(workDir, name), contents, cancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(workDir, "__cf_run.sh"),
                BuildScript(language, compileTimeout, runTimeout), cancellationToken);

            // Container runs as an unprivileged user; it must be able to write build artifacts here.
            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(workDir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
            }

            // Per-phase timeouts are enforced inside the container; the outer timeout is a safety net.
            var outerTimeout = (language.Compile is null ? TimeSpan.Zero : compileTimeout) + runTimeout + SafetyBuffer;
            var run = await RunContainerAsync(image, workDir, request.StandardInput, outerTimeout, cancellationToken, progress);

            return ToOutcome(workDir, run);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static string BuildScript(LanguageDefinition language, TimeSpan compileTimeout, TimeSpan runTimeout)
    {
        var script = new StringBuilder("#!/bin/sh\n");

        if (language.Compile is not null)
        {
            var compileSecs = (int)compileTimeout.TotalSeconds;
            script.AppendLine("start=$(date +%s)");
            script.AppendLine(
                $"timeout -k 2 {compileSecs} {language.Compile.FileName} {language.Compile.Arguments} > __cf_compile.out 2> __cf_compile.err");
            script.AppendLine("code=$?");
            script.AppendLine("if [ $code -ne 0 ]; then");
            script.AppendLine("  end=$(date +%s)");
            // GNU timeout passes through a command's own exit 124, so also check elapsed wall time.
            script.AppendLine($"  if [ $code -eq 124 ] && [ $((end - start)) -ge {compileSecs} ]; then touch {CompileTimeoutMarker}; else touch {CompileFailedMarker}; fi");
            script.AppendLine("  exit 0");
            script.AppendLine("fi");
        }

        var runSecs = (int)runTimeout.TotalSeconds;
        script.AppendLine("start=$(date +%s)");
        script.AppendLine($"timeout -k 2 {runSecs} {language.Run.FileName} {language.Run.Arguments}");
        script.AppendLine("code=$?");
        script.AppendLine("end=$(date +%s)");
        script.AppendLine($"if [ $code -eq 124 ] && [ $((end - start)) -ge {runSecs} ]; then touch {RunTimeoutMarker}; fi");
        script.AppendLine("exit $code");
        return script.ToString();
    }

    private static RunOutcome ToOutcome(string workDir, ProcessOutcome run)
    {
        // Outer safety-net timeout fired (per-phase timeouts inside the container did not).
        if (run.TimedOut)
            return new RunOutcome(ExecutionStatus.TimedOut, run.Stdout, run.Stderr, null, run.Duration);

        if (MarkerExists(workDir, CompileTimeoutMarker))
        {
            return new RunOutcome(
                ExecutionStatus.TimedOut,
                ReadOutputFile(workDir, "__cf_compile.out"),
                ReadOutputFile(workDir, "__cf_compile.err"),
                null, run.Duration);
        }

        if (MarkerExists(workDir, CompileFailedMarker))
        {
            return new RunOutcome(
                ExecutionStatus.CompileError,
                ReadOutputFile(workDir, "__cf_compile.out"),
                ReadOutputFile(workDir, "__cf_compile.err"),
                null, run.Duration);
        }

        if (MarkerExists(workDir, RunTimeoutMarker))
            return new RunOutcome(ExecutionStatus.TimedOut, run.Stdout, run.Stderr, null, run.Duration);

        var status = run.ExitCode == 0 ? ExecutionStatus.Completed : ExecutionStatus.Failed;
        return new RunOutcome(status, run.Stdout, run.Stderr, run.ExitCode, run.Duration);
    }

    private static bool MarkerExists(string workDir, string marker) =>
        File.Exists(Path.Combine(workDir, marker));

    private static string ReadOutputFile(string workDir, string name)
    {
        var path = Path.Combine(workDir, name);
        if (!File.Exists(path))
            return "";

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new char[MaxOutputChars];
        using var reader = new StreamReader(stream);
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    private async Task<ProcessOutcome> RunContainerAsync(
        string image, string workDir, string? standardInput,
        TimeSpan timeout, CancellationToken cancellationToken, IExecutionProgress? progress)
    {
        var containerName = $"codeforge-{Guid.NewGuid():N}";

        var arguments = string.Join(' ',
            "run --rm -i",
            $"--name {containerName}",
            "--network none",
            $"--memory {_options.MemoryMb}m --memory-swap {_options.MemoryMb}m",
            $"--cpus {_options.CpuCount}",
            $"--pids-limit {_options.PidsLimit}",
            "--read-only",
            "--tmpfs /tmp:rw,nosuid,size=64m",
            "--cap-drop ALL",
            "--security-opt no-new-privileges",
            "--user 65534:65534",
            "-e HOME=/tmp",
            "-e DOTNET_CLI_TELEMETRY_OPTOUT=1",
            "-e DOTNET_NOLOGO=1",
            $"-v \"{workDir}:/work\"",
            "-w /work",
            image,
            "sh", "__cf_run.sh");

        return await ProcessRunner.RunAsync(
            "docker", arguments, workDir, standardInput, timeout, cancellationToken,
            onTimeout: () => KillContainerAsync(containerName), progress: progress);
    }

    private static async Task KillContainerAsync(string containerName)
    {
        try
        {
            await ProcessRunner.RunAsync(
                "docker", $"rm -f {containerName}", workDir: null, standardInput: null,
                TimeSpan.FromSeconds(15), CancellationToken.None);
        }
        catch { /* container may already be gone */ }
    }
}
