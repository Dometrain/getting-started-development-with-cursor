using System.Net.Http.Headers;
using System.Text;

namespace LivePoll.Integration.Compose;

[Collection(ComposeGroup.Name)]
public sealed class CallsReachEveryApiReplica(ComposeFixture stack)
{
    [Fact(DisplayName = "CallsReachEveryApiReplica")]
    public async Task ApiCallsThroughNginx_AreHandledByEveryRunningReplica()
    {
        using var client = new HttpClient { BaseAddress = stack.Origin, Timeout = TimeSpan.FromSeconds(30) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline && seen.Count < 2)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/dev-token")
            {
                Content = new StringContent(
                    "{\"username\":\"spread-" + Guid.NewGuid().ToString("N") + "\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
            using var response = await client.SendAsync(request);
            var destination = Destination(response);
            if (destination is "api1" or "api2")
            {
                seen.Add(destination);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        Assert.Contains("api1", seen);
        Assert.Contains("api2", seen);
    }

    private static string? Destination(HttpResponseMessage response)
    {
        if (TryRead(response.Headers, out var destination))
        {
            return destination;
        }

        return TryRead(response.Content.Headers, out destination) ? destination : null;
    }

    private static bool TryRead(HttpHeaders headers, out string? destination)
    {
        if (headers.TryGetValues("X-LivePoll-Destination", out var values))
        {
            destination = values.FirstOrDefault();
            return !string.IsNullOrEmpty(destination);
        }

        destination = null;
        return false;
    }
}