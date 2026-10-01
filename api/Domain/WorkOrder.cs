namespace GridOps.Api.Domain;

public class WorkOrder : ITimestamped
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Notes { get; set; }

    public WorkOrderStatus Status { get; set; }
    public Priority Priority { get; set; }

    public int OutageId { get; set; }
    public Outage Outage { get; set; } = null!;

    // null = not assigned yet
    public int? CrewId { get; set; }
    public Crew? Crew { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
