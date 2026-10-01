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

## API

| Method | Route | Notes |
|---|---|---|
| GET | `/api/outages` | filters: `status`, `priority`, `borough` (repeatable), `from`, `to`. `sortBy` = reportedAt / priority / customersAffected, `sortDir`, `page`, `pageSize` (max 100) |
| GET | `/api/outages/{id}` | includes work orders |
| POST | `/api/outages` | 201 + Location |
| PATCH | `/api/outages/{id}/status` | 409 if already resolved or work orders still open |
| GET | `/api/work-orders` | filters: `status`, `crewId`, `outageId` + paging |
| GET | `/api/work-orders/{id}` | |
| POST | `/api/outages/{id}/work-orders` | 409 if outage resolved |
| PUT | `/api/work-orders/{id}/crew` | `{ "crewId": 5 }` or `null` to unassign |
| PATCH | `/api/work-orders/{id}/status` | InProgress/Completed need a crew. Completed/Cancelled are final |
| GET | `/api/crews` | `borough`, `includeInactive`. includes open work count |

Example: `GET /api/outages?status=Reported&status=Restoring&borough=Brooklyn&sortBy=priority&page=1`

Paged responses: `{ items, page, pageSize, totalCount, totalPages }`

### Errors

All errors are [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) with a `traceId` that matches the server logs.

| Status | When |
|---|---|
| 400 | validation failed. `errors` has camelCase field names |
| 404 | resource not found |
| 409 | valid request but breaks a business rule (e.g. resolving with open work orders) |
| 500 | unexpected. no internals outside Development |

```json
{ "status": 400, "title": "One or more validation errors occurred.",
  "errors": { "pageSize": ["The field PageSize must be between 1 and 100."] }, "traceId": "00-..." }
```

## Performance

Composite indexes on the dashboard filters cut the main dashboard query from 17.9 ms to 1.7 ms and pages read by 98% on 200k outages. Method and numbers in [benchmarks/README.md](benchmarks/README.md).
