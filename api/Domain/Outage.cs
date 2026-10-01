namespace GridOps.Api.Domain;

public class Outage : ITimestamped
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    public Borough Borough { get; set; }
    public required string Neighborhood { get; set; }
    public int CustomersAffected { get; set; }

    public OutageStatus Status { get; set; }
    public Priority Priority { get; set; }

    public DateTimeOffset ReportedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<WorkOrder> WorkOrders { get; set; } = [];
}
