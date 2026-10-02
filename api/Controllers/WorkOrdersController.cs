using GridOps.Api.Auth;
using GridOps.Api.Common.Paging;
using GridOps.Api.Contracts.WorkOrders;
using GridOps.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GridOps.Api.Controllers;

[ApiController]
[Route("api/work-orders")]
[Authorize] // both roles. crew only sees own work - enforced in WorkOrderService
public class WorkOrdersController(IWorkOrderService workOrders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<WorkOrderDto>> List([FromQuery] WorkOrderQuery query, CancellationToken ct)
        => workOrders.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<WorkOrderDto> Get(int id, CancellationToken ct)
        => workOrders.GetAsync(id, ct);

    // nested route - a work order always belongs to an outage
    [HttpPost("/api/outages/{outageId:int}/work-orders")]
    [Authorize(Roles = Roles.Dispatcher)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkOrderDto>> Create(int outageId, CreateWorkOrderRequest request, CancellationToken ct)
    {
        var workOrder = await workOrders.CreateAsync(outageId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = workOrder.Id }, workOrder);
    }

    [HttpPut("{id:int}/crew")]
    [Authorize(Roles = Roles.Dispatcher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCrew(int id, AssignCrewRequest request, CancellationToken ct)
    {
        await workOrders.AssignCrewAsync(id, request.CrewId, ct);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateWorkOrderStatusRequest request, CancellationToken ct)
    {
        await workOrders.UpdateStatusAsync(id, request.Status!.Value, ct);
        return NoContent();
    }
}
