using System.Threading.Channels;

using LivePoll.Application;

namespace LivePoll.Api.LiveResults;

public sealed class ResultsSnapshotHub(IClock clock)
{
    public static readonly TimeSpan MinimumGap = TimeSpan.FromSeconds(1);

    private readonly object gate = new();
    private readonly List<ResultsViewer> subscribers = [];
    private readonly Dictionary<Guid, long> newestToken = [];
    private readonly Dictionary<ResultsViewer, ParsedResultsSnapshot> pending = [];

    public ResultsViewer Subscribe(Guid pollId)
    {
        var viewer = new ResultsViewer(pollId);
        lock (gate)
        {
            viewer.MarkSent(clock.UtcNow);
            subscribers.Add(viewer);
        }

        return viewer;
    }

    public void Unsubscribe(ResultsViewer viewer)
    {
        lock (gate)
        {
            subscribers.Remove(viewer);
            pending.Remove(viewer);
        }

        viewer.Complete();
    }

    public bool AcceptPublishedPayload(string payload)
    {
        if (!PublishedSnapshotParser.TryParse(payload, out var snapshot))
        {
            return false;
        }

        return Accept(snapshot);
    }

    public bool Accept(ParsedResultsSnapshot snapshot)
    {
        List<ResultsViewer> ready = [];
        lock (gate)
        {
            if (newestToken.TryGetValue(snapshot.PollId, out var newest) && snapshot.FencingToken < newest)
            {
                return false;
            }

            newestToken[snapshot.PollId] = snapshot.FencingToken;
            var now = clock.UtcNow;
            foreach (var viewer in subscribers)
            {
                if (viewer.PollId != snapshot.PollId)
                {
                    continue;
                }

                if (viewer.TryTake(now))
                {
                    pending.Remove(viewer);
                    ready.Add(viewer);
                }
                else if (pending.TryAdd(viewer, snapshot))
                {
                    var delay = viewer.DelayUntilDue(now);
                    _ = DeliverPendingAsync(viewer, delay);
                }
                else
                {
                    pending[viewer] = snapshot;
                }
            }
        }

        foreach (var viewer in ready)
        {
            viewer.Enqueue(snapshot);
        }

        return true;
    }

    private async Task DeliverPendingAsync(ResultsViewer viewer, TimeSpan delay)
    {
        while (true)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            ParsedResultsSnapshot held;
            lock (gate)
            {
                if (!pending.TryGetValue(viewer, out var snapshot))
                {
                    return;
                }

                var now = clock.UtcNow;
                if (!viewer.TryTake(now))
                {
                    delay = viewer.DelayUntilDue(now);
                    if (delay <= TimeSpan.Zero)
                    {
                        delay = TimeSpan.FromMilliseconds(1);
                    }

                    continue;
                }

                pending.Remove(viewer);
                held = snapshot;
            }

            viewer.Enqueue(held);
            return;
        }
    }
}

public sealed class ResultsViewer(Guid pollId)
{
    private readonly Channel<ParsedResultsSnapshot> channel =
        Channel.CreateUnbounded<ParsedResultsSnapshot>();

    private DateTimeOffset? lastSent;

    public Guid PollId { get; } = pollId;

    public ChannelReader<ParsedResultsSnapshot> Reader => channel.Reader;

    internal void MarkSent(DateTimeOffset now) => lastSent = now;

    internal bool TryTake(DateTimeOffset now)
    {
        if (DelayUntilDue(now) > TimeSpan.Zero)
        {
            return false;
        }

        lastSent = now;
        return true;
    }

    internal TimeSpan DelayUntilDue(DateTimeOffset now)
    {
        if (lastSent is not DateTimeOffset sent)
        {
            return TimeSpan.Zero;
        }

        var delay = sent + ResultsSnapshotHub.MinimumGap - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    internal void Enqueue(ParsedResultsSnapshot snapshot) => channel.Writer.TryWrite(snapshot);

    internal void Complete() => channel.Writer.TryComplete();
}