using System.Diagnostics;
using System.Text;
using CodeForge.Core.Execution;

namespace CodeForge.Infrastructure.Execution;

internal sealed record ProcessOutcome(
    string Stdout, string Stderr, int? ExitCode, bool TimedOut, TimeSpan Duration);

internal static class ProcessRunner
{
    private const int MaxOutputChars = 64 * 1024;

    public static async Task<ProcessOutcome> RunAsync(
        string fileName, string arguments, string? workDir, string? standardInput,
        TimeSpan timeout, CancellationToken cancellationToken,
        Func<Task>? onTimeout = null, IExecutionProgress? progress = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workDir ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = startInfo };
        var started = Stopwatch.GetTimestamp();
        process.Start();

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
        }

        var stdoutTask = ReadCappedAsync(process.StandardOutput, "stdout", progress, cancellationToken);
        var stderrTask = ReadCappedAsync(process.StandardError, "stderr", progress, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            if (onTimeout is not null)
                await onTimeout();
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }
            await process.WaitForExitAsync(CancellationToken.None);
        }

        return new ProcessOutcome(
            await stdoutTask, await stderrTask,
            timedOut ? null : process.ExitCode,
            timedOut, Stopwatch.GetElapsedTime(started));
    }

    private static async Task<string> ReadCappedAsync(
        StreamReader reader, string stream, IExecutionProgress? progress, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var output = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (progress is not null)
                await progress.OutputAsync(stream, new string(buffer, 0, read));

            if (output.Length < MaxOutputChars)
                output.Append(buffer, 0, Math.Min(read, MaxOutputChars - output.Length));
        }
        return output.ToString();
    }
}
