using LivePoll.Application;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LivePoll.Worker;

public sealed class SnapshotPublisher
{
    public static readonly TimeSpan Lease = TimeSpan.FromMilliseconds(250);

    public static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(1);

    private static readonly Action<ILogger, string, long, Exception?> leaseAcquired =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(1, "PublisherLeaseAcquired"),
            "Publisher lease acquired on {Machine} with fencing token {FencingToken}.");

    private static readonly Action<ILogger, string, long, Exception?> leaseLost =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2, "PublisherLeaseLost"),
            "Publisher lease lost on {Machine} with fencing token {FencingToken}.");

    private static readonly Action<ILogger, string, long, Exception?> publishFailed =
        LoggerMessage.Define<string, long>(
            LogLevel.Error,
            new EventId(3, "SnapshotPublishFailed"),
            "Snapshot publish failed on {Machine} with fencing token {FencingToken}.");

    private readonly IPublishLock publishLock;
    private readonly ILiveTotals liveTotals;
    private readonly IActivePollCleaner activePolls;
    private readonly IResultsPublisher results;
    private readonly ISnapshotFence fence;
    private readonly IClock clock;
    private readonly ILogger<SnapshotPublisher> logger;
    private readonly Dictionary<Guid, DateTimeOffset> lastPublishedAt = [];
    private readonly Dictionary<Guid, string> lastPicture = [];

    public SnapshotPublisher(
        IPublishLock publishLock,
        ILiveTotals liveTotals,
        IActivePollCleaner activePolls,
        IResultsPublisher results,
        ISnapshotFence fence,
        IClock clock,
        ILogger<SnapshotPublisher>? logger = null)
    {
        this.publishLock = publishLock;
        this.liveTotals = liveTotals;
        this.activePolls = activePolls;
        this.results = results;
        this.fence = fence;
        this.clock = clock;
        this.logger = logger ?? NullLogger<SnapshotPublisher>.Instance;
    }

    public async Task PublishOnceAsync(CancellationToken cancellationToken = default)
    {
        var acquired = await publishLock.TryAcquireAsync(Lease, cancellationToken);
        if (acquired is null)
        {
            return;
        }

        await HoldAsync(
            acquired,
            token => PublishWhileHeldAsync(acquired, token),
            cancellationToken);
    }

    public async Task PublishUntilCancelledAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var acquired = await publishLock.TryAcquireAsync(Lease, stoppingToken);
            if (acquired is null)
            {
                await WaitAsync(Lease / 3, stoppingToken);
                continue;
            }

            await HoldAsync(
                acquired,
                async token =>
                {
                    while (!token.IsCancellationRequested && acquired.IsHeld)
                    {
                        await PublishWhileHeldAsync(acquired, token);
                        await WaitAsync(SnapshotInterval, token);
                    }
                },
                stoppingToken);
        }
    }

    private async Task HoldAsync(
        IPublishLockHold acquired,
        Func<CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        leaseAcquired(logger, Environment.MachineName, acquired.FencingToken, null);
        try
        {
            await using (acquired)
            {
                await publish(cancellationToken);
            }
        }
        finally
        {
            leaseLost(logger, Environment.MachineName, acquired.FencingToken, null);
        }
    }

    private async Task PublishWhileHeldAsync(IPublishLockHold hold, CancellationToken cancellationToken)
    {
        _ = await liveTotals.ReadAsync(cancellationToken);
        if (!hold.IsHeld)
        {
            return;
        }

        var now = clock.UtcNow;
        await activePolls.RemoveEndedAsync(now, cancellationToken);

        var current = await liveTotals.ReadAsync(cancellationToken);
        if (!hold.IsHeld)
        {
            return;
        }

        foreach (var picture in current)
        {
            await PublishPictureAsync(hold, picture, now, cancellationToken);
        }
    }

    private static async Task WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PublishPictureAsync(
        IPublishLockHold hold,
        LivePollPicture picture,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (picture.Options.Count < 2)
        {
            return;
        }

        var ended = picture.EndsAt <= now;
        var votesPerSecond = VotesPerSecond.FromStreamEntryIds(
            picture.StreamEntryIds,
            picture.RuvioServerUnixMilliseconds);
        var pictureKey = PictureKey(picture, ended, votesPerSecond);
        if (!ShouldPublish(picture.PollId, pictureKey, now))
        {
            return;
        }

        if (!await fence.TryAcceptAsync(picture.PollId, hold.FencingToken, cancellationToken))
        {
            return;
        }

        var payload = ResultsSnapshotJson.Serialize(picture, ended, votesPerSecond, hold.FencingToken);
        try
        {
            await results.PublishAsync(payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            publishFailed(logger, Environment.MachineName, hold.FencingToken, exception);
            throw;
        }

        lastPublishedAt[picture.PollId] = now;
        lastPicture[picture.PollId] = pictureKey;
    }

    private bool ShouldPublish(Guid pollId, string pictureKey, DateTimeOffset now)
    {
        if (lastPicture.TryGetValue(pollId, out var previous) && previous == pictureKey)
        {
            return false;
        }

        return !lastPublishedAt.TryGetValue(pollId, out var sentAt) || now - sentAt >= SnapshotInterval;
    }

    private static string PictureKey(LivePollPicture picture, bool ended, int votesPerSecond)
    {
        var options = string.Join(
            '|',
            picture.Options.Select(option => $"{option.OptionId:D}:{option.Votes}"));
        return $"{ended}:{votesPerSecond}:{options}";
    }
}