using GridOps.Api.Common.Errors;
using GridOps.Api.Common.Paging;
using GridOps.Api.Contracts.Outages;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using GridOps.Api.Services;
using GridOps.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GridOps.Api.Tests.Infrastructure.TestData;

namespace GridOps.Api.Tests.Services;

public class OutageServiceTests(SqlServerFixture fixture) : DatabaseTest(fixture)
{
    private OutageService Service(GridOpsDbContext db) => new(db, Clock);

    // --- status rules ---

    [Fact]
    public async Task Resolving_with_open_work_orders_is_rejected()
    {
        var crew = Crew();
        var outage = Outage(status: OutageStatus.Restoring);
        await SeedAsync(
            WorkOrder(outage, crew, WorkOrderStatus.InProgress),
            WorkOrder(outage, crew, WorkOrderStatus.Completed));

        await using var db = NewDb();
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Service(db).UpdateStatusAsync(outage.Id, OutageStatus.Resolved, default));

        Assert.Contains("1 open work order", ex.Message);
        await using var check = NewDb();
        Assert.Equal(OutageStatus.Restoring, (await check.Outages.SingleAsync()).Status);
    }

    [Fact]
    public async Task Resolving_sets_resolved_at_when_all_work_is_finished()
    {
        var crew = Crew();
        var outage = Outage(status: OutageStatus.Restoring);
        await SeedAsync(
            WorkOrder(outage, crew, WorkOrderStatus.Completed),
            WorkOrder(outage, crew, WorkOrderStatus.Cancelled));

        await using (var db = NewDb())
            await Service(db).UpdateStatusAsync(outage.Id, OutageStatus.Resolved, default);

        await using var check = NewDb();
        var saved = await check.Outages.SingleAsync();
        Assert.Equal(OutageStatus.Resolved, saved.Status);
        Assert.Equal(Now, saved.ResolvedAt);
    }

    [Fact]
    public async Task Resolved_outage_cannot_be_reopened()
    {
        var outage = Outage(status: OutageStatus.Resolved);
        await SeedAsync(outage);

        await using var db = NewDb();
        await Assert.ThrowsAsync<ConflictException>(
            () => Service(db).UpdateStatusAsync(outage.Id, OutageStatus.Restoring, default));
    }

    [Fact]
    public async Task Create_starts_as_reported_at_current_time()
    {
        await using var db = NewDb();
        var created = await Service(db).CreateAsync(new CreateOutageRequest
        {
            Title = "  Manhole fire - Tribeca  ",
            Borough = Borough.Manhattan,
            Neighborhood = "Tribeca",
            Priority = Priority.Critical,
            CustomersAffected = 4200,
        }, default);

        Assert.Equal("Manhole fire - Tribeca", created.Title); // trimmed
        Assert.Equal(OutageStatus.Reported, created.Status);
        Assert.Equal(Now, created.ReportedAt);
        Assert.Null(created.ResolvedAt);
    }

    [Fact]
    public async Task Get_unknown_outage_throws_not_found()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetAsync(999, default));
    }

    // --- filtering ---

    [Fact]
    public async Task List_combines_status_borough_and_search_filters()
    {
        await SeedAsync(
            Outage("Feeder outage - Astoria", Borough.Queens, "Astoria", OutageStatus.Reported),
            Outage("Feeder outage - Astoria (old)", Borough.Queens, "Astoria", OutageStatus.Resolved),
            Outage("Feeder outage - Flushing", Borough.Queens, "Flushing", OutageStatus.Reported),
            Outage("Feeder outage - Astoria Park", Borough.Brooklyn, "Bushwick", OutageStatus.Reported));

        await using var db = NewDb();
        var result = await Service(db).ListAsync(new OutageQuery
        {
            Status = [OutageStatus.Reported, OutageStatus.Investigating],
            Borough = [Borough.Queens],
            Search = "astoria",
        }, default);

        var match = Assert.Single(result.Items);
        Assert.Equal("Feeder outage - Astoria", match.Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task List_date_range_includes_both_ends()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);
        await SeedAsync(
            Outage("before", reportedAt: from.AddSeconds(-1)),
            Outage("start", reportedAt: from),
            Outage("end", reportedAt: to),
            Outage("after", reportedAt: to.AddSeconds(1)));

        await using var db = NewDb();
        var result = await Service(db).ListAsync(new OutageQuery { From = from, To = to }, default);

        Assert.Equal(["end", "start"], result.Items.Select(o => o.Title)); // newest first
    }

    [Fact]
    public async Task List_counts_only_unfinished_work_orders()
    {
        var crew = Crew();
        var outage = Outage();
        await SeedAsync(
            WorkOrder(outage),
            WorkOrder(outage, crew, WorkOrderStatus.InProgress),
            WorkOrder(outage, crew, WorkOrderStatus.Completed),
            WorkOrder(outage, crew, WorkOrderStatus.Cancelled));

        await using var db = NewDb();
        var result = await Service(db).ListAsync(new OutageQuery(), default);

        Assert.Equal(2, Assert.Single(result.Items).OpenWorkOrders);
    }

    // --- sorting + paging ---

    [Theory]
    [InlineData(SortDirection.Desc, new[] { Priority.Critical, Priority.High, Priority.Medium, Priority.Low })]
    [InlineData(SortDirection.Asc, new[] { Priority.Low, Priority.Medium, Priority.High, Priority.Critical })]
    public async Task Priority_sorts_by_severity_not_alphabetically(SortDirection dir, Priority[] expected)
    {
        // stored as strings - alphabetical would be Critical, High, Low, Medium
        await SeedAsync(
            Outage("a", priority: Priority.Low),
            Outage("b", priority: Priority.Critical),
            Outage("c", priority: Priority.Medium),
            Outage("d", priority: Priority.High));

        await using var db = NewDb();
        var result = await Service(db).ListAsync(
            new OutageQuery { SortBy = OutageSortField.Priority, SortDir = dir }, default);

        Assert.Equal(expected, result.Items.Select(o => o.Priority));
    }

    [Fact]
    public async Task Ties_are_broken_by_id_so_pages_are_stable()
    {
        // everyone has the same customer count -> only the Id tie-breaker decides the order.
        // without it SQL Server returns ties in whatever order it likes, so a row could show on two pages.
        // sorting on an unindexed column on purpose: the ReportedAt index happens to hide the bug
        var outages = Enumerable.Range(1, 5).Select(i => Outage($"o{i}", customers: 500)).ToArray();
        await SeedAsync(outages.Cast<object>().ToArray());

        await using var db = NewDb();
        var service = Service(db);
        var pages = new List<PagedResult<OutageSummaryDto>>();
        for (var page = 1; page <= 3; page++)
            pages.Add(await service.ListAsync(new OutageQuery
            {
                SortBy = OutageSortField.CustomersAffected,
                SortDir = SortDirection.Desc,
                Page = page,
                PageSize = 2,
            }, default));

        var expectedIds = outages.Select(o => o.Id).OrderDescending();
        Assert.Equal(expectedIds, pages.SelectMany(p => p.Items).Select(o => o.Id));
        Assert.All(pages, p => Assert.Equal(5, p.TotalCount));
        Assert.Equal(3, pages[0].TotalPages);
    }
}
