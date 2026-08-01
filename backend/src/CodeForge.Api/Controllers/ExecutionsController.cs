using CodeForge.Api.Contracts;
using CodeForge.Core.Execution;
using CodeForge.Core.Languages;
using Microsoft.AspNetCore.Mvc;

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
            Request = new ExecutionRequest(request.Language, request.SourceCode, request.StandardInput)
        };

        store.Add(record);
        await queue.EnqueueAsync(record, cancellationToken);

        return AcceptedAtAction(
            nameof(GetById), new { id = record.Id },
            new SubmitExecutionResponse(record.Id, record.Status));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ExecutionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var record = store.Get(id);
        return record is null ? NotFound() : Ok(ExecutionResponse.FromRecord(record));
    }
}
