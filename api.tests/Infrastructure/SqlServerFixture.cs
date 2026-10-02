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
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private Respawner _respawner = null!;
    private GridOpsApiFactory? _api;

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

    // in-memory API for endpoint tests, booted on first use and shared
    public GridOpsApiFactory Api => _api ??= new GridOpsApiFactory(ConnectionString);

    public GridOpsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<GridOpsDbContext>().UseSqlServer(ConnectionString).Options);

    // wipes all rows (FK-aware). each test starts from an empty db
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        if (_api is not null) await _api.DisposeAsync();
        await _container.DisposeAsync();
    }
}

// tests in this collection share the container and run one at a time (no shared-db races)
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "database";
}
