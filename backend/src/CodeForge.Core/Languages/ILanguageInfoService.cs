namespace CodeForge.Core.Languages;

public sealed record LanguageInfo(string Id, string DisplayName, string Version, string DocsUrl);

public interface ILanguageInfoService
{
    Task<IReadOnlyList<LanguageInfo>> GetAllAsync(CancellationToken cancellationToken);
}
