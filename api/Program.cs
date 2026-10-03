using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using GridOps.Api.Auth;
using GridOps.Api.Common.Errors;
using GridOps.Api.Data;
using GridOps.Api.Data.Seeding;
using GridOps.Api.Domain;
using GridOps.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// In Development, "ConnectionStrings:GridOps" comes from user secrets (see README).
// In Azure it comes from App Service configuration. It is never committed to appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("GridOps")
    ?? throw new InvalidOperationException("Connection string 'GridOps' is not configured.");

builder.Services.AddDbContext<GridOpsDbContext>(options =>
    // retry transient errors - Azure SQL free tier pauses when idle and drops connections while resuming
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
        maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// auth. signing key from user secrets locally, App Service config in Azure
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey is not configured or shorter than 32 bytes.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub", "role" as-is instead of long ClaimTypes URIs
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.GetSigningKey(),
            NameClaimType = ClaimNames.Name,
            RoleClaimType = ClaimNames.Role,
            ClockSkew = TimeSpan.FromSeconds(30), // default is 5 min of grace after expiry
        };
    });
// secure by default: every endpoint needs a valid token unless marked [AllowAnonymous]
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// behind App Service's front end the TCP peer is the proxy, not the user.
// read the real client IP + scheme from X-Forwarded-*. otherwise every user shares one rate limit bucket
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // App Service proxy IPs aren't fixed. ok because the app is only reachable through that proxy
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// brute force protection: 5 login attempts per minute per IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx.HttpContext,
            ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests", Detail = "Too many login attempts. Try again shortly." },
        });
    };
    options.AddPolicy(RateLimits.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<DevDataSeeder>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// scoped like DbContext - one per request
builder.Services.AddScoped<IOutageService, OutageService>();
builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<ICrewService, CrewService>();
builder.Services.AddSingleton(TimeProvider.System);

// validation error keys use JSON names (pageSize not PageSize)
builder.Services.AddControllers(o => o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// OpenAPI generator reads these options, not the MVC ones above - without it enums show as integers in the spec
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// every error response is ProblemDetails (RFC 9457) + traceId to match logs
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ValidationResponse.Create);
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
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

app.UseForwardedHeaders(); // first, so everything after sees the real IP/scheme
app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 404/405 etc -> ProblemDetails

if (app.Environment.IsDevelopment())
{
    // Built-in generator serves the OpenAPI document; Swashbuckle's UI renders it.
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "GridOps API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// prod: CI copies the Angular build into wwwroot -> one app, one origin, no CORS
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous(); // App Service probes without a token

// unknown /api routes stay a 404, not the Angular page
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous().ExcludeFromDescription();
// anything else -> index.html so a refresh on /dashboard lets Angular's router handle it
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// lets WebApplicationFactory<Program> in api.tests find the entry point
public partial class Program;
