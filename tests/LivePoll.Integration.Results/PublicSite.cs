using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Integration.Results;

internal sealed record CreatedPoll(Guid Id, Guid FirstOptionId);

internal readonly record struct AcceptedVote(string? Destination);

internal static class PublicSite
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] pollOptions = ["Cafeteria", "Food truck"];

    public static HttpClient CreateClient(Uri origin, HttpMessageHandler? handler = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.BaseAddress = origin;
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
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
        Assert.False(string.IsNullOrEmpty(token));
        return token ?? throw new InvalidOperationException("Sign-in did not return a token.");
    }

    public static async Task<CreatedPoll> CreatePollAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = JsonRequest(HttpMethod.Post, "/polls", token, new
        {
            title = "Lunch spot",
            options = pollOptions,
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        return new CreatedPoll(
            document.RootElement.GetProperty("id").GetGuid(),
            document.RootElement.GetProperty("options")[0].GetProperty("id").GetGuid());
    }

    public static async Task<string?> WarmReplicaAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var token = await SignInAsync(client, "warm-" + Guid.NewGuid().ToString("N"), cancellationToken);
        using var request = JsonRequest(HttpMethod.Post, "/polls", token, new
        {
            title = "Lunch spot",
            options = pollOptions,
        });
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return Destination(response);
    }

    public static async Task<AcceptedVote> VoteAsync(
        HttpClient client,
        string token,
        Guid pollId,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        using var request = JsonRequest(
            HttpMethod.Post,
            $"/polls/{pollId}/votes",
            token,
            new { optionId });
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, body);
        return new AcceptedVote(Destination(response));
    }

    public static string? Destination(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-LivePoll-Destination", out var values))
        {
            return values.FirstOrDefault();
        }

        return response.Content.Headers.TryGetValues("X-LivePoll-Destination", out values)
            ? values.FirstOrDefault()
            : null;
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, string? token, object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, options: jsonOptions),
        };
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}