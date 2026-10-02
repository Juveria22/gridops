using GridOps.Api.Auth;
using GridOps.Api.Common.Errors;
using GridOps.Api.Contracts.WorkOrders;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using GridOps.Api.Services;
using GridOps.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static GridOps.Api.Tests.Infrastructure.TestData;

namespace GridOps.Api.Tests.Services;

public class WorkOrderServiceTests(SqlServerFixture fixture) : DatabaseTest(fixture)
{
    private WorkOrderService Service(GridOpsDbContext db, ICurrentUser? user = null) =>
        new(db, Clock, user ?? FakeCurrentUser.Dispatcher());

    private async Task<WorkOrder> Reload(int id)
    {
        await using var db = NewDb();
        return await db.WorkOrders.SingleAsync(w => w.Id == id);
    }

    // --- ownership ---

    [Fact]
    public async Task Crew_member_only_sees_their_own_crews_work()
    {
        var mine = Crew("Manhattan Overhead 1");
        var other = Crew("Brooklyn Network 1", Borough.Brooklyn);
        var outage = Outage();
        await SeedAsync(
            WorkOrder(outage, mine, title: "mine"),
            WorkOrder(outage, other, title: "theirs"),
            WorkOrder(outage, title: "unassigned"));

        await using var db = NewDb();
        var result = await Service(db, FakeCurrentUser.CrewMember(mine.Id)).ListAsync(new WorkOrderQuery(), default);

        Assert.Equal("mine", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task Crew_member_cannot_widen_the_list_with_another_crew_id()
    {
        var mine = Crew("A");
        var other = Crew("B");
        var outage = Outage();
        await SeedAsync(WorkOrder(outage, mine), WorkOrder(outage, other));

        await using var db = NewDb();
        var result = await Service(db, FakeCurrentUser.CrewMember(mine.Id))
            .ListAsync(new WorkOrderQuery { CrewId = other.Id }, default);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Crew_user_without_a_crew_sees_nothing_not_the_unassigned_work()
    {
        // without the null guard, CrewId == null would match every unassigned work order
        await SeedAsync(WorkOrder(Outage()), WorkOrder(Outage()));

        await using var db = NewDb();
        var result = await Service(db, FakeCurrentUser.CrewMember(crewId: null)).ListAsync(new WorkOrderQuery(), default);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Dispatcher_sees_all_work_orders()
    {
        var outage = Outage();
        await SeedAsync(WorkOrder(outage, Crew("A")), WorkOrder(outage, Crew("B")), WorkOrder(outage));

        await using var db = NewDb();
        var result = await Service(db).ListAsync(new WorkOrderQuery(), default);

        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Another_crews_work_order_looks_like_it_does_not_exist()
    {
        var mine = Crew("A");
        var theirs = WorkOrder(Outage(), Crew("B"));
        await SeedAsync(mine, theirs);
        var crewMember = FakeCurrentUser.CrewMember(mine.Id);

        await using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, crewMember).GetAsync(theirs.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(
            () => Service(db, crewMember).UpdateStatusAsync(theirs.Id, WorkOrderStatus.InProgress, default));

        Assert.Equal(WorkOrderStatus.Assigned, (await Reload(theirs.Id)).Status);
    }

    // --- crew status changes ---

    [Fact]
    public async Task Crew_can_start_and_complete_their_own_work()
    {
        var crew = Crew();
        var workOrder = WorkOrder(Outage(), crew);
        await SeedAsync(workOrder);
        var crewMember = FakeCurrentUser.CrewMember(crew.Id);

        await using (var db = NewDb())
            await Service(db, crewMember).UpdateStatusAsync(workOrder.Id, WorkOrderStatus.InProgress, default);
        await using (var db = NewDb())
            await Service(db, crewMember).UpdateStatusAsync(workOrder.Id, WorkOrderStatus.Completed, default);

        var saved = await Reload(workOrder.Id);
        Assert.Equal(WorkOrderStatus.Completed, saved.Status);
        Assert.Equal(Now, saved.CompletedAt);
    }

    [Fact]
    public async Task Crew_cannot_cancel_work()
    {
        var crew = Crew();
        var workOrder = WorkOrder(Outage(), crew);
        await SeedAsync(workOrder);

        await using var db = NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db, FakeCurrentUser.CrewMember(crew.Id))
            .UpdateStatusAsync(workOrder.Id, WorkOrderStatus.Cancelled, default));
    }

    // --- workflow rules ---

    [Fact]
    public async Task Work_cannot_start_without_a_crew()
    {
        var workOrder = WorkOrder(Outage());
        await SeedAsync(workOrder);

        await using var db = NewDb();
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => Service(db).UpdateStatusAsync(workOrder.Id, WorkOrderStatus.InProgress, default));
        Assert.Contains("no crew", ex.Message);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Completed)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public async Task Finished_work_orders_are_locked(WorkOrderStatus finished)
    {
        var crew = Crew();
        var workOrder = WorkOrder(Outage(), crew, finished);
        await SeedAsync(workOrder);

        await using var db = NewDb();
        await Assert.ThrowsAsync<ConflictException>(
            () => Service(db).UpdateStatusAsync(workOrder.Id, WorkOrderStatus.InProgress, default));
        await Assert.ThrowsAsync<ConflictException>(() => Service(db).AssignCrewAsync(workOrder.Id, null, default));
    }

    [Fact]
    public async Task Assigning_and_unassigning_a_crew_moves_status_between_open_and_assigned()
    {
        var crew = Crew();
        var workOrder = WorkOrder(Outage());
        await SeedAsync(crew, workOrder);

        await using (var db = NewDb())
            await Service(db).AssignCrewAsync(workOrder.Id, crew.Id, default);
        var assigned = await Reload(workOrder.Id);
        Assert.Equal((WorkOrderStatus.Assigned, crew.Id), (assigned.Status, assigned.CrewId));

        await using (var db = NewDb())
            await Service(db).AssignCrewAsync(workOrder.Id, null, default);
        var unassigned = await Reload(workOrder.Id);
        Assert.Equal((WorkOrderStatus.Open, (int?)null), (unassigned.Status, unassigned.CrewId));
    }

    [Fact]
    public async Task Crew_cannot_be_removed_while_work_is_in_progress()
    {
        var workOrder = WorkOrder(Outage(), Crew(), WorkOrderStatus.InProgress);
        await SeedAsync(workOrder);

        await using var db = NewDb();
        await Assert.ThrowsAsync<ConflictException>(() => Service(db).AssignCrewAsync(workOrder.Id, null, default));
    }

    [Fact]
    public async Task Unknown_or_inactive_crew_is_a_validation_error_on_crewId()
    {
        var inactive = Crew("Retired crew", active: false);
        var workOrder = WorkOrder(Outage());
        await SeedAsync(inactive, workOrder);

        await using var db = NewDb();
        var unknown = await Assert.ThrowsAsync<InvalidRequestException>(
            () => Service(db).AssignCrewAsync(workOrder.Id, 9999, default));
        var retired = await Assert.ThrowsAsync<InvalidRequestException>(
            () => Service(db).AssignCrewAsync(workOrder.Id, inactive.Id, default));

        Assert.Equal("crewId", unknown.Field);
        Assert.Contains("inactive", retired.Message);
    }

    [Fact]
    public async Task New_work_order_inherits_outage_priority_and_is_assigned_when_crew_given()
    {
        var crew = Crew();
        var outage = Outage(priority: Priority.Critical);
        await SeedAsync(crew, outage);

        await using var db = NewDb();
        var created = await Service(db).CreateAsync(outage.Id,
            new CreateWorkOrderRequest { Title = "Replace transformer", CrewId = crew.Id }, default);

        Assert.Equal(Priority.Critical, created.Priority);
        Assert.Equal(WorkOrderStatus.Assigned, created.Status);
        Assert.Equal(crew.Name, created.CrewName);
        Assert.Equal(outage.Title, created.OutageTitle);
    }

    [Fact]
    public async Task Work_cannot_be_added_to_a_resolved_outage()
    {
        var outage = Outage(status: OutageStatus.Resolved);
        await SeedAsync(outage);

        await using var db = NewDb();
        await Assert.ThrowsAsync<ConflictException>(() => Service(db)
            .CreateAsync(outage.Id, new CreateWorkOrderRequest { Title = "Too late" }, default));
    }
}
