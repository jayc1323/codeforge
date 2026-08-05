using System.Security.Claims;
using CodeForge.Api.Contracts;
using CodeForge.Core.Execution;
using CodeForge.Core.Languages;
using CodeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecutionsController(IExecutionQueue queue, IExecutionStore store) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SubmitExecutionResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(SubmitExecutionRequest request, CancellationToken cancellationToken)
    {
        if (LanguageRegistry.Find(request.Language) is null)
            return BadRequest(new { error = $"Unsupported language: {request.Language}" });

        var record = new ExecutionRecord
        {
            Id = Guid.NewGuid(),
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Request = new ExecutionRequest(request.Language, request.SourceCode, request.StandardInput)
        };

        await store.AddAsync(record, cancellationToken);
        await queue.EnqueueAsync(record, cancellationToken);

        return AcceptedAtAction(
            nameof(GetById), new { id = record.Id },
            new SubmitExecutionResponse(record.Id, record.Status));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ExecutionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(id, cancellationToken);
        return record is null ? NotFound() : Ok(ExecutionResponse.FromRecord(record));
    }

    [Authorize]
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(
        [FromServices] IDbContextFactory<CodeForgeDbContext> contextFactory,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var executions = await context.Executions
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new
            {
                e.Id,
                e.Language,
                e.Status,
                e.ExitCode,
                e.DurationMs,
                e.CreatedAt,
                e.CompletedAt,
                SourcePreview = e.SourceCode.Substring(0, Math.Min(200, e.SourceCode.Length))
            })
            .ToListAsync(cancellationToken);

        return Ok(executions);
    }
}
