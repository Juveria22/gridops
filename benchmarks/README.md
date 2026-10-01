# Dashboard query benchmark

Measures the dashboard queries before/after the `AddOutageIndexes` migration.

## Setup

Separate `GridOps_Bench` database so dev data isn't touched. 200,000 outages (~400k work orders), same seeder as dev.

```bash
# connection string = your dev one with Database=GridOps_Bench
dotnet ef database update InitialCreate --project api --connection "<bench connection string>"
dotnet run --project api -- seed --outages 200000 "--ConnectionStrings:GridOps=<bench connection string>"
```

## Run

```bash
docker cp benchmarks/dashboard-queries.sql gridops-sql:/tmp/bench.sql
docker exec gridops-sql sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d GridOps_Bench -i /tmp/bench.sql -o /tmp/out.txt; tail -8 /tmp/out.txt'
```

Then `dotnet ef database update --project api --connection "<bench connection string>"` to apply indexes and run again.

## Method

- each query runs 50x, parameterized via `sp_executesql` like EF Core
- avg elapsed time + logical reads from `sys.dm_exec_query_stats` (server side, no network)
- warm cache: table fits in memory, so this measures CPU/pages read, not disk
- logical reads = 8 KB pages touched. deterministic, better for comparing than ms
- 3 runs each, numbers below are the median run
- SQL Server 2022 Developer in Docker (WSL 2), laptop

## Results (200k outages)

### Before indexes

| Query | Avg ms | Logical reads |
|---|---|---|
| active_by_borough | 10.14 | 10,406 |
| active_by_borough_count | 7.76 | 10,406 |
| recent_high_priority | 8.15 | 10,406 |
| recent_high_priority_count | 7.44 | 10,406 |
| all_newest | 18.28 | 10,406 |

Every query is a full clustered index scan (whole table).

### After `AddOutageIndexes`

| Query | Avg ms | Logical reads | Index used |
|---|---|---|---|
| active_by_borough | 0.09 | 108 | `IX_Outages_Borough_ReportedAt` seek + key lookups |
| active_by_borough_count | 1.65 | 225 | `IX_Outages_Status_ReportedAt` seek (Borough included, no lookups) |
| recent_high_priority | 0.24 | 438 | `IX_Outages_ReportedAt` seek + key lookups |
| recent_high_priority_count | 0.37 | 42 | `IX_Outages_Priority_ReportedAt` seek |
| all_newest | 0.09 | 83 | `IX_Outages_ReportedAt` ordered scan, stops after 25 rows |

### Summary

- active outages by borough (list + count = one dashboard load): 17.9 ms -> 1.7 ms (~10x), 20,812 -> 333 pages read (-98%)
- newest-first default view: 18.3 ms -> 0.09 ms
- no query scans the whole table anymore
- index used = actual cached plan (`sys.dm_exec_query_plan`), not assumed
- TOP 25 list queries: optimizer prefers an index already sorted by ReportedAt so it can stop after 25 matches. counts use the narrower equality-first index
