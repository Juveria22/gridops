using GridOps.Api.Domain;

namespace GridOps.Api.Tests.Infrastructure;

// valid entities with defaults. tests only set what they care about
public static class TestData
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static Crew Crew(string name = "Manhattan Overhead 1", Borough borough = Borough.Manhattan, bool active = true) =>
        new() { Name = name, HomeBorough = borough, IsActive = active };

    public static Outage Outage(
        string title = "Transformer failure - Midtown",
        Borough borough = Borough.Manhattan,
        string neighborhood = "Midtown",
        OutageStatus status = OutageStatus.Reported,
        Priority priority = Priority.Medium,
        DateTimeOffset? reportedAt = null,
        int customers = 100) =>
        new()
        {
            Title = title,
            Borough = borough,
            Neighborhood = neighborhood,
            Status = status,
            Priority = priority,
            ReportedAt = reportedAt ?? BaseTime,
            CustomersAffected = customers,
        };

    public static WorkOrder WorkOrder(
        Outage outage,
        Crew? crew = null,
        WorkOrderStatus? status = null,
        string title = "Site assessment") =>
        new()
        {
            Outage = outage,
            Crew = crew,
            Title = title,
            Priority = outage.Priority,
            Status = status ?? (crew is null ? WorkOrderStatus.Open : WorkOrderStatus.Assigned),
        };
}
