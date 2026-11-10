using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LivePoll.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<HealthEndpointTests.ApiFactory>
{
    private readonly HttpClient client;

    public HealthEndpointTests(ApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Get_ReturnsHealthy()
    {
        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:Issuer", ApiWebApplicationFactory.Issuer);
            builder.UseSetting("Jwt:Audience", ApiWebApplicationFactory.Audience);
            builder.UseSetting("Jwt:SigningKey", ApiWebApplicationFactory.SigningKey);
        }
    }
}