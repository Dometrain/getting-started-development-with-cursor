using System.Globalization;

using LivePoll.Api.Metrics;
using LivePoll.Application;
using LivePoll.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Ruvio.AspNetCore.RateLimiting;
using Ruvio.Client;

namespace LivePoll.Api.Tests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "livepoll-dev-signing-key-32bytes!";
    public const string Issuer = "livepoll";
    public const string Audience = "livepoll";
    public const string AdminUser = "ada-admin";

    public InMemoryPollRepository Polls { get; } = new();

    public RecordingPollEndTimeReader EndTimes { get; } = new();

    public RecordingOneVoteMarker OneVoteMarker { get; } = new();

    public RecordingVoteQueue VoteQueue { get; } = new();

    public RecordingAcceptanceMetrics AcceptanceMetrics { get; } = new();

    public ManualBusinessMetricsStore Metrics { get; } = new();

    public ManualActivePollDirectory ActivePolls { get; } = new();

    public PostgresTouchGuard PostgresTouches { get; } = new();

    public MutableClock Clock { get; } = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public RuvioOutageMode OutageMode { get; set; } = RuvioOutageMode.FailOpen;

    public string RuvioHost { get; set; } = "127.0.0.1";

    public int RuvioPort { get; set; } = 1;

    public string RuvioPassword { get; set; } = "unused";

    public int VotePermits { get; set; } = 100;

    public int VoteWindowSeconds { get; set; } = 60;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Admin:Users", AdminUser);
        builder.UseSetting("Ruvio:Host", RuvioHost);
        builder.UseSetting("Ruvio:Port", RuvioPort.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Ruvio:Password", RuvioPassword);
        builder.UseSetting("VoteRateLimit:Permits", VotePermits.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("VoteRateLimit:WindowSeconds", VoteWindowSeconds.ToString(CultureInfo.InvariantCulture));
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey,
                ["Admin:Users"] = AdminUser,
                ["Ruvio:Host"] = RuvioHost,
                ["Ruvio:Port"] = RuvioPort.ToString(CultureInfo.InvariantCulture),
                ["Ruvio:Password"] = RuvioPassword,
                ["VoteRateLimit:Permits"] = VotePermits.ToString(CultureInfo.InvariantCulture),
                ["VoteRateLimit:WindowSeconds"] = VoteWindowSeconds.ToString(CultureInfo.InvariantCulture),
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPollRepository>();
            services.AddSingleton<IPollRepository>(Polls);

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IPollEndTimeReader>();
            services.RemoveAll<IOneVoteMarker>();
            services.RemoveAll<IVoteQueue>();
            services.RemoveAll<IAcceptanceMetrics>();
            services.RemoveAll<VoteAcceptanceService>();

            services.AddSingleton<IPollEndTimeReader>(EndTimes);
            services.AddSingleton<IPollEndTimeWriter>(EndTimes);
            services.AddSingleton<IActivePollWriter, RecordingActivePollWriter>();
            services.AddSingleton<IOneVoteMarker>(OneVoteMarker);
            services.AddSingleton<IVoteQueue>(VoteQueue);
            services.AddSingleton<IAcceptanceMetrics>(AcceptanceMetrics);
            services.AddSingleton<VoteAcceptanceService>();

            services.RemoveAll<IBusinessMetricsStore>();
            services.AddSingleton<IBusinessMetricsStore>(Metrics);
            services.RemoveAll<IActivePollDirectory>();
            services.AddSingleton<IActivePollDirectory>(ActivePolls);

            services.AddSingleton(PostgresTouches);
            services.RemoveAll<DbContextOptions<LivePollDbContext>>();
            services.RemoveAll<LivePollDbContext>();
            services.AddScoped<LivePollDbContext>(_ =>
            {
                PostgresTouches.Touch();
                throw new InvalidOperationException("Postgres must not be opened on the vote accept path.");
            });

            services.PostConfigure<RuvioRateLimitingOptions>(options =>
            {
                options.Outage.Mode = OutageMode;
            });
        });
    }
}

public sealed class RuvioBackedApiFactory : ApiWebApplicationFactory
{
    public RuvioBackedApiFactory(RuvioApiFixture ruvio)
    {
        OutageMode = RuvioOutageMode.FailClosed;
        RuvioHost = "127.0.0.1";
        RuvioPort = ruvio.Port;
        RuvioPassword = RuvioApiFixture.Password;
        VotePermits = 100;
        VoteWindowSeconds = 60;
    }
}

public sealed class InMemoryPollRepository : IPollRepository
{
    private readonly Dictionary<Guid, StoredPoll> items = new();

    public Task SaveAsync(StoredPoll poll, CancellationToken cancellationToken)
    {
        items[poll.Aggregate.Id] = poll;
        return Task.CompletedTask;
    }

    public Task<StoredPoll?> GetAsync(Guid pollId, CancellationToken cancellationToken)
    {
        items.TryGetValue(pollId, out var poll);
        return Task.FromResult(poll);
    }
}