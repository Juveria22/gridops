using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GridOps.Api.Auth;
using GridOps.Api.Common.Paging;
using GridOps.Api.Contracts.Auth;
using GridOps.Api.Contracts.Outages;
using GridOps.Api.Domain;
using GridOps.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static GridOps.Api.Tests.Infrastructure.TestData;

namespace GridOps.Api.Tests.Api;

// real HTTP through the whole pipeline: routing, JWT, [Authorize], validation, exception handler, JSON
public class ApiEndpointTests(SqlServerFixture fixture) : DatabaseTest(fixture)
{
    private const string Password = "Test-Password-1!";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private User _dispatcher = null!;
    private User _crewMember = null!;
    private Crew _crew = null!;

    private async Task SeedUsersAsync()
    {
        var hasher = new PasswordHasher<User>();
        _crew = Crew();
        _dispatcher = new User { Email = "dispatcher@test.com", DisplayName = "Dispatcher", Role = UserRole.Dispatcher };
        _crewMember = new User { Email = "crew@test.com", DisplayName = "Crew", Role = UserRole.Crew, Crew = _crew };
        _dispatcher.PasswordHash = hasher.HashPassword(_dispatcher, Password);
        _crewMember.PasswordHash = hasher.HashPassword(_crewMember, Password);
        await SeedAsync(_dispatcher, _crewMember);
    }

    // token from the API's own TokenService - same key and claims as /login, without hitting the login rate limit
    private HttpClient ClientFor(User? user)
    {
        var client = Fixture.Api.CreateClient();
        if (user is null) return client;

        using var scope = Fixture.Api.Services.CreateScope();
        var (token, _) = scope.ServiceProvider.GetRequiredService<TokenService>().Create(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out _));
        return problem;
    }

    // --- authentication / authorization ---

    [Theory]
    [InlineData("/api/outages")]
    [InlineData("/api/work-orders")]
    [InlineData("/api/crews")]
    [InlineData("/api/auth/me")]
    public async Task Endpoints_require_a_token(string url)
    {
        var response = await ClientFor(null).GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_check_is_public()
    {
        var response = await ClientFor(null).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/outages")]
    [InlineData("GET", "/api/outages/1")]
    [InlineData("GET", "/api/crews")]
    [InlineData("POST", "/api/outages")]
    [InlineData("PUT", "/api/work-orders/1/crew")]
    public async Task Crew_members_are_forbidden_from_dispatcher_endpoints(string method, string url)
    {
        await SeedUsersAsync();

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method != "GET") request.Content = JsonContent.Create(new { });
        var response = await ClientFor(_crewMember).SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        await SeedUsersAsync();
        var client = ClientFor(_crewMember);
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var parts = token.Split('.');
        // swap the payload for one claiming Dispatcher, keep the old signature
        var forged = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new { sub = "2", role = "Dispatcher" }))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{parts[0]}.{forged}.{parts[2]}");

        var response = await client.GetAsync("/api/outages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_a_token_that_works_on_protected_endpoints()
    {
        await SeedUsersAsync();
        var client = ClientFor(null);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "crew@test.com", password = Password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me", Json);

        Assert.Equal("crew@test.com", me!.Email);
        Assert.Equal(_crew.Id, me.CrewId);
    }

    [Fact]
    public async Task Bad_login_is_a_401_problem_response()
    {
        await SeedUsersAsync();

        var response = await ClientFor(null).PostAsJsonAsync("/api/auth/login",
            new { email = "crew@test.com", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", (await ProblemAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_is_rate_limited_per_client()
    {
        // fresh app instance -> fresh rate limiter, doesn't affect other tests
        await using var api = new GridOpsApiFactory(Fixture.ConnectionString);
        var client = api.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "x@test.com", password = "x" });
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                Assert.True(response.Headers.Contains("Retry-After"));
        }

        Assert.All(statuses.Take(5), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);
    }

    [Fact]
    public async Task Rate_limit_is_per_real_client_ip_behind_a_proxy()
    {
        // App Service's front end forwards the user's IP in X-Forwarded-For.
        // without forwarded headers, every user would share the proxy's bucket
        await using var api = new GridOpsApiFactory(Fixture.ConnectionString);
        var client = api.CreateClient();

        async Task<HttpStatusCode> LoginFrom(string ip)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "x@test.com", password = "x" }),
            };
            request.Headers.Add("X-Forwarded-For", ip);
            return (await client.SendAsync(request)).StatusCode;
        }

        for (var i = 0; i < 5; i++) await LoginFrom("203.0.113.10");

        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginFrom("203.0.113.10"));
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginFrom("198.51.100.20")); // different user, not blocked
    }

    // --- filtering over HTTP ---

    [Fact]
    public async Task Outage_filters_bind_from_the_query_string()
    {
        await SeedUsersAsync();
        await SeedAsync(
            Outage("Astoria - reported", Borough.Queens, "Astoria", OutageStatus.Reported, Priority.Low),
            Outage("Astoria - restoring", Borough.Queens, "Astoria", OutageStatus.Restoring, Priority.Critical),
            Outage("Astoria - resolved", Borough.Queens, "Astoria", OutageStatus.Resolved, Priority.High),
            Outage("Bushwick - reported", Borough.Brooklyn, "Bushwick", OutageStatus.Reported, Priority.High));

        var result = await ClientFor(_dispatcher).GetFromJsonAsync<PagedResult<OutageSummaryDto>>(
            "/api/outages?status=Reported&status=Restoring&borough=Queens&sortBy=priority&sortDir=desc", Json);

        Assert.Equal(["Astoria - restoring", "Astoria - reported"], result!.Items.Select(o => o.Title));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Enums_are_strings_in_json()
    {
        await SeedUsersAsync();
        await SeedAsync(Outage(borough: Borough.StatenIsland, priority: Priority.Critical));

        var json = await ClientFor(_dispatcher).GetStringAsync("/api/outages");

        Assert.Contains("\"borough\":\"StatenIsland\"", json);
        Assert.Contains("\"priority\":\"Critical\"", json);
    }

    [Fact]
    public async Task Invalid_query_is_a_400_with_camelCase_field_errors()
    {
        await SeedUsersAsync();

        var response = await ClientFor(_dispatcher).GetAsync("/api/outages?pageSize=500&status=Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("pageSize", out _));
        Assert.True(errors.TryGetProperty("status", out _));
    }

    // --- ownership + business rules over HTTP ---

    [Fact]
    public async Task Crew_gets_404_for_another_crews_work_order()
    {
        await SeedUsersAsync();
        var theirs = WorkOrder(Outage(), Crew("Other crew"));
        await SeedAsync(theirs);

        var response = await ClientFor(_crewMember).GetAsync($"/api/work-orders/{theirs.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rule_violation_is_a_409_problem_with_the_reason()
    {
        await SeedUsersAsync();
        var outage = Outage(status: OutageStatus.Restoring);
        await SeedAsync(WorkOrder(outage, Crew("Busy crew"), WorkOrderStatus.InProgress));

        var response = await ClientFor(_dispatcher).PatchAsJsonAsync(
            $"/api/outages/{outage.Id}/status", new { status = "Resolved" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("open work order", (await ProblemAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Creating_an_outage_returns_201_with_location()
    {
        await SeedUsersAsync();

        var response = await ClientFor(_dispatcher).PostAsJsonAsync("/api/outages", new
        {
            title = "Manhole fire - Tribeca",
            borough = "Manhattan",
            neighborhood = "Tribeca",
            priority = "Critical",
            customersAffected = 4200,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OutageDetailDto>(Json);
        Assert.EndsWith($"/api/outages/{created!.Id}", response.Headers.Location!.ToString());
    }
}
