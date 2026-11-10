using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LivePoll.Bff.Tests;

public sealed class BffWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "livepoll-dev-signing-key-32bytes!";
    public const string Issuer = "livepoll";
    public const string Audience = "livepoll";

    private WebApplication? api1Host;
    private WebApplication? api2Host;
    private WebApplication? simulatorHost;
    private WebApplication? workerHost;
    private int apiHits;
    private int simulatorHits;
    private int workerHits;

    public int ApiHits
    {
        get => Volatile.Read(ref apiHits);
        set => Volatile.Write(ref apiHits, value);
    }

    public int SimulatorHits
    {
        get => Volatile.Read(ref simulatorHits);
        set => Volatile.Write(ref simulatorHits, value);
    }

    public int WorkerHits
    {
        get => Volatile.Read(ref workerHits);
        set => Volatile.Write(ref workerHits, value);
    }

    public string Api1Address { get; private set; } = "";
    public string Api2Address { get; private set; } = "";
    public string SimulatorAddress { get; private set; } = "";
    public string WorkerAddress { get; private set; } = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        StartDestinations();

        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey,
                ["ReverseProxy:Clusters:api:Destinations:api1:Address"] = Api1Address,
                ["ReverseProxy:Clusters:api:Destinations:api2:Address"] = Api2Address,
                ["ReverseProxy:Clusters:api-results:Destinations:api1:Address"] = Api1Address,
                ["ReverseProxy:Clusters:api-results:Destinations:api2:Address"] = Api2Address,
                ["ReverseProxy:Clusters:simulator:Destinations:simulator:Address"] = SimulatorAddress,
            });
        });
    }

    private void StartDestinations()
    {
        api1Host = StartHost("api1", () => Interlocked.Increment(ref apiHits));
        api2Host = StartHost("api2", () => Interlocked.Increment(ref apiHits));
        simulatorHost = StartHost("simulator", () => Interlocked.Increment(ref simulatorHits));
        workerHost = StartHost("worker", () => Interlocked.Increment(ref workerHits));

        Api1Address = api1Host.Urls.Single();
        Api2Address = api2Host.Urls.Single();
        SimulatorAddress = simulatorHost.Urls.Single();
        WorkerAddress = workerHost.Urls.Single();
    }

    private static WebApplication StartHost(string name, Action onHit)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = name,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.MapMethods(
            "/{**catch-all}",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
            async (HttpContext context) =>
            {
                onHit();
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync(name);
            });
        app.StartAsync().GetAwaiter().GetResult();
        return app;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (api1Host is not null)
        {
            await api1Host.DisposeAsync();
        }

        if (api2Host is not null)
        {
            await api2Host.DisposeAsync();
        }

        if (simulatorHost is not null)
        {
            await simulatorHost.DisposeAsync();
        }

        if (workerHost is not null)
        {
            await workerHost.DisposeAsync();
        }
    }
}