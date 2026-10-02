using GridOps.Api.Data;
using Microsoft.Extensions.Time.Testing;

namespace GridOps.Api.Tests.Infrastructure;

[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTest(SqlServerFixture fixture) : IAsyncLifetime
{
    protected static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    // frozen clock -> exact timestamps in asserts
    protected FakeTimeProvider Clock { get; } = new(Now);

    // new context per arrange/act/assert, like separate HTTP requests.
    // stops tests passing only because EF already had the entity in memory
    protected GridOpsDbContext NewDb() => fixture.CreateDbContext();

    protected async Task SeedAsync(params object[] entities)
    {
        await using var db = NewDb();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
