using System.Diagnostics;
using System.Text.RegularExpressions;
using CodeForge.Core.Languages;
using Microsoft.Extensions.Logging;

namespace CodeForge.Infrastructure.Languages;

public sealed partial class LanguageInfoService(ILogger<LanguageInfoService> logger) : ILanguageInfoService
{
    private static readonly TimeSpan DetectionTimeout = TimeSpan.FromSeconds(10);
    private Task<IReadOnlyList<LanguageInfo>>? _cached;

    public Task<IReadOnlyList<LanguageInfo>> GetAllAsync(CancellationToken cancellationToken) =>
        _cached ??= DetectAllAsync(cancellationToken);

    private async Task<IReadOnlyList<LanguageInfo>> DetectAllAsync(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(LanguageRegistry.All.Select(async language =>
            new LanguageInfo(language.Id, language.DisplayName,
                await DetectVersionAsync(language, cancellationToken))));

        return results;
    }

    private async Task<string> DetectVersionAsync(LanguageDefinition language, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = language.VersionCommand.FileName,
                    Arguments = language.VersionCommand.Arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DetectionTimeout);
            await process.WaitForExitAsync(timeoutCts.Token);

            var firstLine = (await process.StandardOutput.ReadLineAsync()) ?? "";
            if (string.IsNullOrWhiteSpace(firstLine))
                firstLine = (await process.StandardError.ReadLineAsync()) ?? "";

            return VersionPattern().Match(firstLine) is { Success: true } match
                ? match.Value
                : "unknown";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Version detection failed for {Language}", language.Id);
            return "unknown";
        }
    }

    [GeneratedRegex(@"\d+(\.\d+)+")]
    private static partial Regex VersionPattern();
}
