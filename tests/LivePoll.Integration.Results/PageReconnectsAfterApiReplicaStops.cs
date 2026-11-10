using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class PageReconnectsAfterApiReplicaStops(ResultsFixture stack)
{
    [Fact(DisplayName = "PageReconnectsAfterApiReplicaStops")]
    public async Task StoppedReplica_SamePublicStreamShowsALaterSnapshot()
    {
        await stack.EnsureReplicasAsync();
        using var client = PublicSite.CreateClient(stack.Origin);
        var token = await PublicSite.SignInAsync(client, "page-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);
        var first = await LiveResultsStream.OpenAsync(client, poll.Id, CancellationToken.None);
        var stopped = first.Destination;
        Assert.True(stopped is "api1" or "api2");
        _ = await first.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        try
        {
            await stack.KillAsync(stopped!);
            await WaitUntilEndedAsync(first);

            await using var reconnected = await ReconnectAsync(client, poll.Id, stopped!);
            Assert.NotEqual(stopped, reconnected.Destination);
            Assert.True(reconnected.Destination is "api1" or "api2");

            await VoteWhenAcceptedAsync(client, token, poll.Id, poll.FirstOptionId);
            var started = Stopwatch.GetTimestamp();
            _ = await reconnected.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(1), CancellationToken.None);
            var elapsed = Stopwatch.GetElapsedTime(started);
            Assert.True(elapsed <= TimeSpan.FromSeconds(1), $"Reconnected viewer saw the vote after {elapsed}.");
        }
        finally
        {
            await first.DisposeAsync();
            await stack.EnsureReplicasAsync();
        }
    }

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task VoteWhenAcceptedAsync(HttpClient client, string token, Guid pollId, Guid optionId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        string last = "";
        while (DateTime.UtcNow < deadline)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/polls/{pollId}/votes");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { optionId }, options: jsonOptions);
            using var response = await client.SendAsync(request);
            last = $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"Vote was not accepted after the replica stopped. Last response: {last}");
    }

    private static async Task WaitUntilEndedAsync(LiveResultsStream stream)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            }
            catch (ResultsStreamEndedException)
            {
                return;
            }
            catch (TimeoutException)
            {
            }
        }

        throw new TimeoutException("The results stream stayed open after its API replica stopped.");
    }

    private static async Task<LiveResultsStream> ReconnectAsync(HttpClient client, Guid pollId, string stopped)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var stream = await LiveResultsStream.OpenAsync(client, pollId, CancellationToken.None);
                if (stream.Destination != stopped)
                {
                    _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                    return stream;
                }

                await stream.DisposeAsync();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        throw new TimeoutException("The page did not open a results stream on the remaining API replica.");
    }
}