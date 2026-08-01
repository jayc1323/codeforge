using CodeForge.Infrastructure.Languages;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeForge.Tests;

public class LanguageInfoServiceTests
{
    [Fact]
    public async Task DetectsVersionsForAllLanguages()
    {
        var service = new LanguageInfoService(NullLogger<LanguageInfoService>.Instance);

        var languages = await service.GetAllAsync(CancellationToken.None);

        Assert.NotEmpty(languages);
        Assert.All(languages, language =>
            Assert.Matches(@"\d+\.\d+", language.Version));
    }

    [Fact]
    public async Task ResultsAreCached()
    {
        var service = new LanguageInfoService(NullLogger<LanguageInfoService>.Instance);

        var first = await service.GetAllAsync(CancellationToken.None);
        var second = await service.GetAllAsync(CancellationToken.None);

        Assert.Same(first, second);
    }
}
