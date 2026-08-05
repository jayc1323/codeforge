using System.Security.Claims;
using CodeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SnippetsController(IDbContextFactory<CodeForgeDbContext> contextFactory) : ControllerBase
{
    public sealed record SaveSnippetRequest(string Title, string Language, string SourceCode, string? StandardInput);
    public sealed record SnippetSummary(Guid Id, string Title, string Language, DateTimeOffset UpdatedAt);
    public sealed record SnippetResponse(
        Guid Id, string Title, string Language, string SourceCode, string? StandardInput,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var snippets = await context.Snippets
            .AsNoTracking()
            .Where(s => s.UserId == CurrentUserId())
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SnippetSummary(s.Id, s.Title, s.Language, s.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(snippets);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var snippet = await context.Snippets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == CurrentUserId(), cancellationToken);

        return snippet is null
            ? NotFound()
            : Ok(new SnippetResponse(
                snippet.Id, snippet.Title, snippet.Language, snippet.SourceCode,
                snippet.StandardInput, snippet.CreatedAt, snippet.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveSnippetRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });

        var snippet = new SnippetEntity
        {
            Id = Guid.NewGuid(),
            UserId = CurrentUserId()!,
            Title = request.Title.Trim(),
            Language = request.Language,
            SourceCode = request.SourceCode,
            StandardInput = request.StandardInput
        };

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Snippets.Add(snippet);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = snippet.Id },
            new SnippetResponse(snippet.Id, snippet.Title, snippet.Language, snippet.SourceCode,
                snippet.StandardInput, snippet.CreatedAt, snippet.UpdatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await context.Snippets
            .Where(s => s.Id == id && s.UserId == CurrentUserId())
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
