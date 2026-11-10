using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using LivePoll.Simulator;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LivePoll.Simulator.Tests;

public sealed class SimulationLimitTests : IClassFixture<SimulatorWebApplicationFactory>
{
    private static readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private static readonly Guid optionA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid optionB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SimulatorWebApplicationFactory factory;

    public SimulationLimitTests(SimulatorWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Start_VoterCountAbove2000_Returns400ProblemJson()
    {
        using var client = factory.CreateClient();
        var storedBefore = factory.StoredCount;
        var startedBefore = factory.Runner.Started.Count;

        using var response = await client.PostAsJsonAsync(
            "/admin/simulations",
            Request(voterCount: 2001, durationSeconds: 1));

        await AssertInvalidAsync(response);
        Assert.Equal(storedBefore, factory.StoredCount);
        Assert.Equal(startedBefore, factory.Runner.Started.Count);
    }

    [Fact]
    public async Task Start_DurationAbove600Seconds_Returns400ProblemJson()
    {
        using var client = factory.CreateClient();
        var storedBefore = factory.StoredCount;
        var startedBefore = factory.Runner.Started.Count;

        using var response = await client.PostAsJsonAsync(
            "/admin/simulations",
            Request(voterCount: 1, durationSeconds: 601));

        await AssertInvalidAsync(response);
        Assert.Equal(storedBefore, factory.StoredCount);
        Assert.Equal(startedBefore, factory.Runner.Started.Count);
    }

    [Fact]
    public async Task Start_AtVoterAndDurationLimits_Returns201()
    {
        using var client = factory.CreateClient();
        var storedBefore = factory.StoredCount;
        var startedBefore = factory.Runner.Started.Count;

        using var response = await client.PostAsJsonAsync(
            "/admin/simulations",
            Request(voterCount: 2000, durationSeconds: 600));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var simulation = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        Assert.NotNull(simulation);
        Assert.Equal(2000, simulation.VoterCount);
        Assert.Equal(600, simulation.DurationSeconds);
        Assert.Contains(simulation.Id.ToString(), response.Headers.Location!.OriginalString, StringComparison.Ordinal);

        Assert.Equal(startedBefore + 1, factory.Runner.Started.Count);
        Assert.Equal(storedBefore + 1, factory.StoredCount);
        var started = factory.Runner.Started[^1];
        Assert.Equal(2000, started.Votes.Count);
        Assert.Equal(600, started.DurationSeconds);
    }

    private static object Request(int voterCount, int durationSeconds) => new
    {
        pollId,
        voterCount,
        rampUpSeconds = 0,
        durationSeconds,
        voteSplit = new[]
        {
            new { optionId = optionA, percent = 50 },
            new { optionId = optionB, percent = 50 },
        },
        seed = 42,
    };

    private static async Task AssertInvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("/problems/invalid-request", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/admin/simulations", document.RootElement.GetProperty("instance").GetString());
    }
}

public sealed class RecordingRunner : ISimulationRunner, IDisposable
{
    private readonly HttpClient client = new();

    public List<VotePlan> Started { get; } = [];

    public SimulationRun Start(Guid pollId, VotePlan plan, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Started.Add(plan);
        return SimulationRun.NotStarted(client, plan);
    }

    public void Dispose() => client.Dispose();
}

public sealed class SimulatorWebApplicationFactory : WebApplicationFactory<Program>
{
    public RecordingRunner Runner { get; } = new();

    public int StoredCount => Services.GetRequiredService<SimulationCatalog>().StoredCount;

    static SimulatorWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("PublicApi__BaseAddress", "http://127.0.0.1:9/");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("PublicApi:BaseAddress", "http://127.0.0.1:9/");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PublicApi:BaseAddress"] = "http://127.0.0.1:9/",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISimulationRunner>();
            services.AddSingleton<ISimulationRunner>(Runner);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Runner.Dispose();
        }

        base.Dispose(disposing);
    }
}