namespace GridOps.Api.Domain;

public class Crew : ITimestamped
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Borough HomeBorough { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<WorkOrder> WorkOrders { get; set; } = [];
    public List<User> Members { get; set; } = [];
}
