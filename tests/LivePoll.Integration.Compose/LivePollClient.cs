using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Integration.Compose;

internal sealed record CreatedPoll(Guid Id, Guid FirstOptionId, Guid SecondOptionId);

internal sealed class LivePollClient : IDisposable
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] pollOptions = ["Cafeteria", "Food truck"];

    private readonly HttpClient client;

    public LivePollClient(HttpClient client)
    {
        this.client = client;
    }

    public static LivePollClient ForOrigin(Uri origin) =>
        new(new HttpClient { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) });

    public void Dispose() => client.Dispose();

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(3);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                _ = await SignInAsync("health-check", cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                last = exception;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        throw new TimeoutException("nginx did not reach the API.", last);
    }

    public async Task<string> SignInAsync(string username, CancellationToken cancellationToken)
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

    public async Task<CreatedPoll> CreatePollAsync(string token, CancellationToken cancellationToken)
    {
        using var request = JsonRequest(
            HttpMethod.Post,
            "/polls",
            token,
            new { title = "Lunch spot", options = pollOptions });
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        var options = document.RootElement.GetProperty("options");
        return new CreatedPoll(
            document.RootElement.GetProperty("id").GetGuid(),
            options[0].GetProperty("id").GetGuid(),
            options[1].GetProperty("id").GetGuid());
    }

    public async Task VoteAsync(string token, Guid pollId, Guid optionId, CancellationToken cancellationToken)
    {
        using var request = JsonRequest(
            HttpMethod.Post,
            $"/polls/{pollId}/votes",
            token,
            new { optionId });
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, body);
    }

    public async Task<long> OptionVotesAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"/polls/{pollId}/results", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        foreach (var option in document.RootElement.GetProperty("options").EnumerateArray())
        {
            if (option.GetProperty("optionId").GetGuid() == optionId)
            {
                return option.GetProperty("votes").GetInt64();
            }
        }

        throw new InvalidOperationException("The option was not in the results.");
    }

    public async Task WaitForVotesAsync(
        Guid pollId,
        Guid optionId,
        long minimum,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        long votes = 0;
        while (DateTime.UtcNow < deadline)
        {
            votes = await OptionVotesAsync(pollId, optionId, cancellationToken);
            if (votes >= minimum)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        Assert.True(votes >= minimum, $"Option {optionId} had {votes} votes.");
    }

    public async Task<HttpResponseMessage> GetMetricsAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, cancellationToken);
    }

    public async Task<HttpResponseMessage> StartSimulationAsync(
        string? token,
        CreatedPoll poll,
        CancellationToken cancellationToken)
    {
        using var request = JsonRequest(
            HttpMethod.Post,
            "/admin/simulations",
            token,
            new
            {
                pollId = poll.Id,
                voterCount = 1,
                rampUpSeconds = 0,
                durationSeconds = 1,
                voteSplit = new[]
                {
                    new { optionId = poll.FirstOptionId, percent = 100 },
                    new { optionId = poll.SecondOptionId, percent = 0 },
                },
                seed = 7,
            });
        return await client.SendAsync(request, cancellationToken);
    }

    public Task<string> GetStringAsync(string path, CancellationToken cancellationToken) =>
        client.GetStringAsync(path, cancellationToken);

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