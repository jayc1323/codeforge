using CodeForge.Api.Contracts;
using CodeForge.Core.Languages;
using Microsoft.AspNetCore.Mvc;

namespace CodeForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LanguagesController(ILanguageInfoService languageInfo) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<LanguageResponse>> GetAll(CancellationToken cancellationToken)
    {
        var languages = await languageInfo.GetAllAsync(cancellationToken);
        return languages.Select(l => new LanguageResponse(l.Id, l.DisplayName, l.Version));
    }
}
