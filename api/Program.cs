using GridOps.Api.Data;
using GridOps.Api.Data.Seeding;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// In Development, "ConnectionStrings:GridOps" comes from user secrets (see README).
// In Azure it comes from App Service configuration. It is never committed to appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("GridOps")
    ?? throw new InvalidOperationException("Connection string 'GridOps' is not configured.");

builder.Services.AddDbContext<GridOpsDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<DevDataSeeder>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GridOpsDbContext>("database");

var app = builder.Build();

// dotnet run --project api -- seed [--outages 500]
if (args.Contains("seed"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Seeding is only allowed in Development.");

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
    await seeder.SeedAsync(app.Configuration.GetValue("outages", 500));
    return;
}

if (app.Environment.IsDevelopment())
{
    // Built-in generator serves the OpenAPI document; Swashbuckle's UI renders it.
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "GridOps API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
