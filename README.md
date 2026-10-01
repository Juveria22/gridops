# GridOps

Outage and work order tracker for an electric utility. Dispatchers log outages and assign work orders to field crews; crews see and update their own work.

## Stack

- **API:** ASP.NET Core Web API (.NET 10 LTS), Entity Framework Core, SQL Server
- **Client:** Angular (standalone components), Angular Material, RxJS
- **Tests:** xUnit
- **Local infra:** SQL Server 2022 in Docker

## Repo layout

```
/api            ASP.NET Core Web API
/api.tests      xUnit tests
/client         Angular app
docker-compose.yml   Local SQL Server
```

## Local setup

Prerequisites: .NET 10 SDK, Node 22+, Docker Desktop (WSL 2 backend on Windows).

1. **Start SQL Server**

   ```bash
   cp .env.example .env        # then edit MSSQL_SA_PASSWORD
   docker compose up -d
   ```

2. **Set the connection string with user secrets** (kept out of source control, stored under your user profile):

   ```bash
   dotnet user-secrets set "ConnectionStrings:GridOps" \
     "Server=localhost,1433;Database=GridOps;User Id=sa;Password=<your password>;TrustServerCertificate=True;Encrypt=True" \
     --project api
   ```

3. **Create the database and seed dev data**

   ```bash
   dotnet tool restore                          # installs dotnet-ef (pinned in dotnet-tools.json)
   dotnet ef database update --project api      # applies migrations, creates GridOps db
   dotnet run --project api -- seed             # ~500 outages across NYC boroughs (Development only)
   ```

   Seed is skipped if outages already exist. `--outages N` for a different size.

4. **Run the API**

   ```bash
   dotnet run --project api --launch-profile http
   ```

   - Swagger UI: http://localhost:5257/swagger
   - OpenAPI document: http://localhost:5257/openapi/v1.json
   - Health (includes a database check): http://localhost:5257/health

## Data model

- **Outage**: borough, neighborhood, customers affected, status, priority, reported/resolved times. Has many work orders.
- **WorkOrder**: belongs to one outage, optionally assigned to one crew.
- **Crew**: field team with a home borough. Has members (users) and work orders.
- **User**: dispatcher or crew member. Auth added in phase 4.

Enums are stored as strings. All timestamps are UTC `datetimeoffset`.

## Performance

Composite indexes on the dashboard filters cut the main dashboard query from 17.9 ms to 1.7 ms and pages read by 98% on 200k outages. Method and numbers in [benchmarks/README.md](benchmarks/README.md).
