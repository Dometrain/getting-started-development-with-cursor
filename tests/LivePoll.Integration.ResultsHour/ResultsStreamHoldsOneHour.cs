using System.Diagnostics;
using System.Net;

namespace LivePoll.Integration.ResultsHour;

public sealed class ResultsStreamHoldsOneHour
{
    [Fact(DisplayName = "ResultsStreamHoldsOneHour")]
    public async Task Stream_ThroughNginxAndBff_EventsKeepArriving()
    {
        var hold = ResultsHold.Duration();
        using var site = await NginxSite.StartAsync(CancellationToken.None);
        var poll = await site.CreatePollAsync(CancellationToken.None);
        await using var stream = await ResultsConnection.OpenAsync(site.Client, poll.Id, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
        Assert.Equal("text/event-stream", stream.ContentType);
        Assert.True(stream.Destination is "api1" or "api2", stream.Destination ?? "");

        await stream.ReadEventAsync(TimeSpan.FromSeconds(60), CancellationToken.None);
        var started = Stopwatch.GetTimestamp();
        var events = 1;
        while (Stopwatch.GetElapsedTime(started) < hold)
        {
            await site.VoteAsync(poll.Id, poll.FirstOptionId, CancellationToken.None);
            await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            events++;
        }

        Assert.True(stream.StillOpen, "The results stream closed before the hold ended.");
        Assert.True(events >= 2, $"Only {events} results events arrived.");
    }
}