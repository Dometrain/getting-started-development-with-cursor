using System.Diagnostics;

namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class VoteOnOneReplicaVisibleOnAnother(ResultsFixture stack)
{
    [Fact(DisplayName = "VoteOnOneReplicaVisibleOnAnother")]
    public async Task VoteOnOneReplica_UpdatesTheViewerOnTheOther()
    {
        await stack.EnsureReplicasAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var token = await PublicSite.SignInAsync(client, "cross-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);
        await using var streams = await OpenBothReplicasAsync(client, poll.Id);
        await Task.WhenAll(
            streams.Api1.ReadEventAsync(TimeSpan.FromSeconds(3), CancellationToken.None),
            streams.Api2.ReadEventAsync(TimeSpan.FromSeconds(3), CancellationToken.None));

        var vote = await PublicSite.VoteAsync(client, token, poll.Id, poll.FirstOptionId, CancellationToken.None);
        Assert.True(vote.Destination is "api1" or "api2");
        var viewer = vote.Destination == "api1" ? streams.Api2 : streams.Api1;
        Assert.NotEqual(vote.Destination, viewer.Destination);
        var started = Stopwatch.GetTimestamp();
        _ = await viewer.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(1), CancellationToken.None);

        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.True(elapsed <= TimeSpan.FromSeconds(1), $"Other replica showed the vote after {elapsed}.");
    }

    private static async Task<ReplicaStreams> OpenBothReplicasAsync(HttpClient client, Guid pollId)
    {
        var opened = new List<LiveResultsStream>();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                var stream = await LiveResultsStream.OpenAsync(client, pollId, CancellationToken.None);
                opened.Add(stream);
                _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                var api1 = opened.FirstOrDefault(candidate => candidate.Destination == "api1");
                var api2 = opened.FirstOrDefault(candidate => candidate.Destination == "api2");
                if (api1 is not null && api2 is not null)
                {
                    opened.Remove(api1);
                    opened.Remove(api2);
                    return new ReplicaStreams(api1, api2);
                }
            }

            throw new TimeoutException("The results stream did not reach both API replicas.");
        }
        finally
        {
            foreach (var stream in opened)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private sealed class ReplicaStreams(LiveResultsStream api1, LiveResultsStream api2) : IAsyncDisposable
    {
        public LiveResultsStream Api1 { get; } = api1;

        public LiveResultsStream Api2 { get; } = api2;

        public async ValueTask DisposeAsync()
        {
            await Api1.DisposeAsync();
            await Api2.DisposeAsync();
        }
    }
}