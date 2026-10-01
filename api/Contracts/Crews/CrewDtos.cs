using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.Crews;

// for the "assign to" dropdown. OpenWorkOrders = current workload
public record CrewDto(int Id, string Name, Borough HomeBorough, bool IsActive, int Members, int OpenWorkOrders);

// ?borough=Queens&includeInactive=true
public class CrewQuery
{
    public Borough? Borough { get; init; }
    public bool IncludeInactive { get; init; }
}
