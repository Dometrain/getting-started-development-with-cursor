using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Integration.ResultsHour;

internal readonly record struct CreatedPoll(Guid Id, Guid FirstOptionId);

internal sealed class NginxSite : IDisposable
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] pollOptions = ["Cafeteria", "Food truck"];

    private NginxSite(HttpClient client)
    {
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<NginxSite> StartAsync(CancellationToken cancellationToken)
    {
        var root = RepoFile.Root();
        var up = await ComposeDocker.ComposeAsync(root, TimeSpan.FromMinutes(5), cancellationToken, "up", "-d", "--wait");
        if (up.ExitCode != 0)
        {
            throw new InvalidOperationException(up.Stderr + up.Stdout);
        }

        var mapped = (await ComposeDocker.ComposeTextAsync(
            root,
            TimeSpan.FromMinutes(2),
            cancellationToken,
            "port",
            "nginx",
            "80")).Trim();
        var port = mapped.Split(':')[^1];
        var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new NginxSite(client);
    }

    public async Task<CreatedPoll> CreatePollAsync(CancellationToken cancellationToken)
    {
        var token = await SignInAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/polls")
        {
            Content = JsonContent.Create(new { title = "Lunch spot", options = pollOptions }, options: jsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new CreatedPoll(
            root.GetProperty("id").GetGuid(),
            root.GetProperty("options")[0].GetProperty("id").GetGuid());
    }

    public async Task VoteAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken)
    {
        var token = await SignInAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/polls/{pollId}/votes")
        {
            Content = JsonContent.Create(new { optionId }, options: jsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, body);
    }

    public void Dispose()
    {
        Client.Dispose();
    }

    private async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username = "hour-" + Guid.NewGuid().ToString("N") },
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
}