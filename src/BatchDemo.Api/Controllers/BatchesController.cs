using BatchDemo.Api.Contracts;
using BatchDemo.Application;
using Microsoft.AspNetCore.Mvc;

namespace BatchDemo.Api.Controllers;

[ApiController]
[Route("api/batches")]
public sealed class BatchesController(BatchIntakeService intakeService, BatchQueryService queryService,
    BatchResultService resultService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<BatchResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromForm] BatchUploadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = request.File?.OpenReadStream();
            var result = await intakeService.IntakeAsync(
                request.MerchantId,
                request.File?.FileName,
                stream,
                cancellationToken);
            var statusUrl = Url.ActionLink(nameof(Get), values: new { batchId = result.BatchId })
                ?? $"/api/batches/{result.BatchId:D}";
            var response = BatchResponse.From(result, statusUrl);
            return AcceptedAtAction(nameof(Get), new { batchId = result.BatchId }, response);
        }
        catch (IntakeValidationException exception)
        {
            ModelState.AddModelError(exception.Field, exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("{batchId:guid}")]
    [ProducesResponseType<BatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchResponse>> Get(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await queryService.FindAsync(batchId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        var statusUrl = Url.ActionLink(nameof(Get), values: new { batchId })
            ?? $"/api/batches/{batchId:D}";
        return Ok(BatchResponse.From(result, statusUrl));
    }

    [HttpGet("{batchId:guid}/results")]
    [ProducesResponseType<BatchPortalResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BatchPortalResult>> GetResults(Guid batchId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await resultService.FindAsync(batchId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (BatchResultUnavailableException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Batch results unavailable",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }
}
