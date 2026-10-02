using GridOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Testcontainers.MsSql;

namespace GridOps.Api.Tests.Infrastructure;

// one throwaway SQL Server container for the whole test run.
// real engine + real migrations - EF InMemory/SQLite don't behave like SQL Server
public class SqlServerFixture : IAsyncLifetime
{
    // same image as docker-compose -> already pulled locally
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "GridOpsTests",
        }.ConnectionString;

        await using (var db = CreateDbContext())
            await db.Database.MigrateAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public GridOpsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<GridOpsDbContext>().UseSqlServer(ConnectionString).Options);

    // wipes all rows (FK-aware). each test starts from an empty db
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

// tests in this collection share the container and run one at a time (no shared-db races)
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "database";
}
