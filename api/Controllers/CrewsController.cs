using GridOps.Api.Contracts.Crews;
using GridOps.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GridOps.Api.Controllers;

[ApiController]
[Route("api/crews")]
public class CrewsController(ICrewService crews) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CrewDto>> List([FromQuery] CrewQuery query, CancellationToken ct)
        => crews.ListAsync(query, ct);
}
