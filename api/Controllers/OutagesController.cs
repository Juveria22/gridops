using GridOps.Api.Common.Paging;
using GridOps.Api.Contracts.Outages;
using GridOps.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GridOps.Api.Controllers;

// [ApiController]: auto 400 on invalid input, binds body from JSON
[ApiController]
[Route("api/outages")]
public class OutagesController(IOutageService outages) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<OutageSummaryDto>> List([FromQuery] OutageQuery query, CancellationToken ct)
        => outages.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OutageDetailDto> Get(int id, CancellationToken ct)
        => outages.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OutageDetailDto>> Create(CreateOutageRequest request, CancellationToken ct)
    {
        var outage = await outages.CreateAsync(request, ct);
        // 201 + Location header pointing at GET /api/outages/{id}
        return CreatedAtAction(nameof(Get), new { id = outage.Id }, outage);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOutageStatusRequest request, CancellationToken ct)
    {
        await outages.UpdateStatusAsync(id, request.Status!.Value, ct);
        return NoContent();
    }
}
