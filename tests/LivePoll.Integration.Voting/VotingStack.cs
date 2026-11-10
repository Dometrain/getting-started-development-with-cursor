using System.Globalization;

using LivePoll.Application;
using LivePoll.Infrastructure;
using LivePoll.Infrastructure.Persistence;
using LivePoll.Infrastructure.Ruvio;
using LivePoll.Worker;
using LivePoll.Worker.Voting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvio.Client;

namespace LivePoll.Integration.Voting;

public sealed class VoteFlowFixture : IAsyncLifetime, IAsyncDisposable
{
    private readonly PostgresSession postgres = new();

    private readonly RuvioSession ruvio = new();

    private readonly List<RuvioConnection> extraConnections = [];

    private ServiceProvider? provider;

    private RuvioConnection? blockingConnection;

    private IRuvioClient? blockingClient;

    private bool disposed;

    public IRuvioClient Client => ruvio.Client;

    public string RuvioContainerId => ruvio.ContainerId;

    public string PostgresContainerId => postgres.ContainerId;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), ruvio.StartAsync());
        provider = new ServiceCollection().AddInfrastructure(postgres.ConnectionString).BuildServiceProvider();
        await MigrateAsync();
        await OpenBlockingClientAsync();
    }

    public VoteAcceptanceService Acceptance(IClock clock, IRuvioClient? client = null)
    {
        var commands = client ?? Client;
        return new VoteAcceptanceService(
            new RuvioPollEndTimes(commands),
            new RuvioOneVoteMarker(commands),
            new RuvioVoteQueue(commands),
            new RuvioVoteCounters(commands),
            clock);
    }

    public async Task<OpenPoll> CreatePollAsync(IClock clock, CancellationToken cancellationToken = default)
    {
        var lifecycle = new PollLifecycleService(
            new PostgresPollRepository(Provider),
            clock,
            new RuvioPollEndTimes(Client),
            new RuvioActivePolls(Client));
        var view = await lifecycle.CreateAsync(
            "ada",
            "Lunch spot",
            ["Cafeteria", "Food truck"],
            cancellationToken);
        return new OpenPoll(
            view.Id,
            view.CreatedAt,
            view.EndsAt,
            view.Options.Select(option => new OpenOption(option.Id, option.Text)).ToArray());
    }

    public async Task StorePendingAsync(IClock clock, CancellationToken cancellationToken = default)
    {
        var source = new RuvioVoteDeliverySource(Client, BlockingClient);
        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
        var processor = new VoteStoreProcessor(
            source,
            new PostgresVoteStore(db, clock),
            new RuvioBacklogCounter(new RuvioVoteCounters(Client)));
        await processor.ProcessBatchAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VoteRecord>> ReadVotesAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
        return await db.Votes.AsNoTracking().Where(vote => vote.PollId == pollId).ToListAsync(cancellationToken);
    }

    public async Task<long> OnceExistsAsync(Guid pollId, string username, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Client.ExistsAsync(RuvioKeys.Once(pollId, username));
    }

    public async Task<int> QueuedForPollAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var entries = await Client.GetStreamRangeAsync(
            RuvioKeys.VoteStream,
            StreamIds.Min,
            StreamIds.Max,
            false,
            cancellationToken);
        return entries.Count(entry => Field(entry, RuvioKeys.PollIdField) == pollId.ToString("D"));
    }

    public async Task<IRuvioClient> OpenClientAsync()
    {
        var connection = await RuvioSession.OpenConnectionAsync(ruvio.Port);
        extraConnections.Add(connection);
        return await connection.GetDefaultDatabaseAsync();
    }

    public async Task<BusinessMetricsView> ReadMetricsAsync(
        IClock clock,
        OpenPoll poll,
        CancellationToken cancellationToken = default)
    {
        var service = new BusinessMetricsService(new RuvioBusinessMetricsStore(Client), clock);
        return await service.GetAsync(
            [
                new OptionMetricQuery(poll.Id, poll.Options[0].Id, poll.Options[0].Text),
                new OptionMetricQuery(poll.Id, poll.Options[1].Id, poll.Options[1].Text),
            ],
            cancellationToken);
    }

    public async Task<int> PublishingWorkerVotesPerSecondAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var totals = new RuvioLiveTotals(
            Client,
            new RuvioBusinessMetricsStore(Client),
            new PostgresPollOptionSource(Provider.GetRequiredService<IServiceScopeFactory>()));
        var pictures = await totals.ReadAsync(cancellationToken);
        var picture = pictures.Single(item => item.PollId == pollId);
        return VotesPerSecond.FromStreamEntryIds(picture.StreamEntryIds, picture.RuvioServerUnixMilliseconds);
    }

    public async Task WaitUntilEntryIsInPreviousSecondAsync(string entryId, CancellationToken cancellationToken = default)
    {
        var entryMilliseconds = EntryMilliseconds(entryId);
        var nextSecond = (entryMilliseconds - (entryMilliseconds % 1000)) + 1000;
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            var store = new RuvioBusinessMetricsStore(Client);
            var (_, serverNow) = await store.GetVoteStreamEntryIdsAsync(cancellationToken);
            if (serverNow >= nextSecond + 250)
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("Ruvio server time did not pass the vote's second.");
    }

    public async Task<string> SingleEntryIdAsync(Guid pollId, CancellationToken cancellationToken = default)
    {
        var entries = await Client.GetStreamRangeAsync(
            RuvioKeys.VoteStream,
            StreamIds.Min,
            StreamIds.Max,
            false,
            cancellationToken);
        var match = entries.Single(entry => Field(entry, RuvioKeys.PollIdField) == pollId.ToString("D"));
        return match.Id;
    }

    public async Task RestartRuvioAsync()
    {
        await DisposeExtrasAsync();
        if (blockingConnection is not null)
        {
            await blockingConnection.DisposeAsync();
            blockingConnection = null;
            blockingClient = null;
        }

        var containerId = ruvio.ContainerId;
        await ruvio.RestartAsync();
        if (!string.Equals(containerId, ruvio.ContainerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ruvio restart replaced the container and dropped its data directory.");
        }

        await OpenBlockingClientAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DisposeExtrasAsync();
        if (blockingConnection is not null)
        {
            await blockingConnection.DisposeAsync();
        }

        if (provider is not null)
        {
            await provider.DisposeAsync();
        }

        await ruvio.DisposeAsync();
        await postgres.DisposeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

    private ServiceProvider Provider => provider ?? throw new InvalidOperationException("Voting stack is not started.");

    private IRuvioClient BlockingClient => blockingClient ?? throw new InvalidOperationException("Ruvio blocking client is not open.");

    private async Task MigrateAsync()
    {
        const int attempts = 30;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await using var scope = Provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception) when (attempt < attempts)
            {
                await Task.Delay(500);
            }
        }
    }

    private async Task OpenBlockingClientAsync()
    {
        blockingConnection = await RuvioSession.OpenConnectionAsync(ruvio.Port);
        blockingClient = await blockingConnection.GetDefaultDatabaseAsync();
    }

    private async Task DisposeExtrasAsync()
    {
        foreach (var connection in extraConnections)
        {
            await connection.DisposeAsync();
        }

        extraConnections.Clear();
    }

    private static string Field(StreamEntry entry, string name) =>
        entry.Fields.Single(pair => pair.Key == name).Value;

    private static long EntryMilliseconds(string entryId)
    {
        var separator = entryId.IndexOf('-');
        if (separator <= 0
            || !long.TryParse(entryId.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
        {
            throw new InvalidOperationException("Ruvio stream id had no server time.");
        }

        return milliseconds;
    }
}