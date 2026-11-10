using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Integration.Load;

internal sealed record CreatedPoll(Guid Id, Guid FirstOptionId, Guid SecondOptionId);

internal readonly record struct CastVote(
    HttpStatusCode Status,
    string? Destination,
    TimeSpan Elapsed,
    string? Error = null);

internal static class PublicSite
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] lunchOptions = ["Cafeteria", "Food truck"];
    private static readonly string[] secondOptions = ["Soup", "Salad"];

    public static HttpClient CreateClient(Uri origin)
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 1000,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false,
            AllowAutoRedirect = false,
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = origin,
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public static async Task<string> SignInAsync(HttpClient client, string username, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username },
            jsonOptions,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var token = document.RootElement.GetProperty("token").GetString();
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Sign-in did not return a token.");
        }

        return token;
    }

    public static async Task<CreatedPoll> CreatePollAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/polls",
            token,
            new { title = "Lunch spot", options = lunchOptions },
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        var options = document.RootElement.GetProperty("options");
        return new CreatedPoll(
            document.RootElement.GetProperty("id").GetGuid(),
            options[0].GetProperty("id").GetGuid(),
            options[1].GetProperty("id").GetGuid());
    }

    public static async Task<CastVote> VoteAsync(
        HttpClient client,
        string token,
        Guid pollId,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        var measured = await MeasureVoteAsync(client, token, pollId, optionId, cancellationToken);
        if (measured.Error is not null)
        {
            throw new InvalidOperationException($"Vote returned {(int)measured.Status}: {measured.Error}");
        }

        return measured;
    }

    public static async Task<CastVote> MeasureVoteAsync(
        HttpClient client,
        string token,
        Guid pollId,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        using var request = JsonRequest(HttpMethod.Post, $"/polls/{pollId}/votes", token, new { optionId });
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = response.StatusCode == HttpStatusCode.Accepted ? null : body;
            return new CastVote(response.StatusCode, Destination(response), elapsed, error);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new CastVote(0, null, Stopwatch.GetElapsedTime(started), exception.Message);
        }
    }

    public static async Task<(int VotesPerSecond, long Backlog)> ReadMetricsAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Metrics returned {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return (
            ReadInt(root, "votesPerSecond", "VotesPerSecond"),
            ReadInt64(root, "votesWaitingForPostgres", "VotesWaitingForPostgres"));
    }

    public static async Task<string?> StartSimulationAsync(
        HttpClient client,
        string adminToken,
        Guid pollId,
        Guid firstOptionId,
        Guid secondOptionId,
        int voterCount,
        int durationSeconds,
        int seed,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/admin/simulations",
            adminToken,
            new
            {
                pollId,
                voterCount,
                rampUpSeconds = 0,
                durationSeconds,
                voteSplit = new[]
                {
                    new { optionId = firstOptionId, percent = 50 },
                    new { optionId = secondOptionId, percent = 50 },
                },
                seed,
            },
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return Destination(response);
    }

    public static async Task<string> OtherApiAsync(
        HttpClient client,
        string token,
        string firstDestination,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var response = await SendAsync(
                client,
                HttpMethod.Post,
                "/polls",
                token,
                new { title = "Second spot", options = secondOptions },
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.Created, body);
            var destination = Destination(response);
            if (destination is "api1" or "api2" && destination != firstDestination)
            {
                return destination;
            }
        }

        throw new TimeoutException($"No call with the same token reached an API other than {firstDestination}.");
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = JsonRequest(method, path, token, body);
        request.Headers.ConnectionClose = true;
        return await client.SendAsync(request, cancellationToken);
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string token, object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: jsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static int ReadInt(JsonElement root, string camel, string pascal) =>
        ReadProperty(root, camel, pascal).GetInt32();

    private static long ReadInt64(JsonElement root, string camel, string pascal) =>
        ReadProperty(root, camel, pascal).GetInt64();

    private static JsonElement ReadProperty(JsonElement root, string camel, string pascal) =>
        root.TryGetProperty(camel, out var value) ? value : root.GetProperty(pascal);

    private static string? Destination(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-LivePoll-Destination", out var values))
        {
            return values.FirstOrDefault();
        }

        return response.Content.Headers.TryGetValues("X-LivePoll-Destination", out values)
            ? values.FirstOrDefault()
            : null;
    }
}