using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GridOps.Api.Tests.Infrastructure;

// the real API (Program.cs, full pipeline) running in memory against the test container
public class GridOpsApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // not Development -> no user secrets, no Swagger, no seed command
        builder.UseEnvironment("Testing");

        // UseSetting (not ConfigureAppConfiguration) so Program.cs sees these when it reads config at startup
        builder.UseSetting("ConnectionStrings:GridOps", connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes");
    }
}
