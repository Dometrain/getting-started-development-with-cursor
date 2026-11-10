using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LivePoll.Api.Tests;

internal static class ResultsApi
{
    private static readonly string[] lunchOptions = ["Cafeteria", "Food truck"];

    public static async Task<CreatedPoll> CreatePollAsync(HttpClient client)
    {
        var token = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = lunchOptions });
        response.EnsureSuccessStatusCode();
        var poll = await response.Content.ReadFromJsonAsync<CreatedPoll>();
        Assert.NotNull(poll);
        client.DefaultRequestHeaders.Authorization = null;
        return poll;
    }

    public static async Task<string> SignInAsync(HttpClient client, string username)
    {
        using var response = await client.PostAsJsonAsync("/auth/dev-token", new { username });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);
        return body.Token;
    }

    public static async Task<OpenResultsStream> OpenStreamAsync(HttpClient client, Guid pollId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/polls/{pollId}/results/stream");
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var stream = await response.Content.ReadAsStreamAsync();
        return new OpenResultsStream(response, new SseEventReader(stream));
    }

    public static string PublishedSnapshot(
        Guid pollId,
        Guid optionA,
        Guid optionB,
        long votesA,
        long votesB,
        int votesPerSecond,
        long fencingToken,
        bool ended = false) =>
        JsonSerializer.Serialize(new
        {
            pollId,
            ended,
            votesPerSecond,
            fencingToken,
            options = new[]
            {
                new { optionId = optionA, text = "Cafeteria", votes = votesA },
                new { optionId = optionB, text = "Food truck", votes = votesB },
            },
        });

    private sealed record DevTokenBody(string Token, string Username);
}

internal sealed record CreatedOption(Guid Id, string Text);

internal sealed record CreatedPoll(
    Guid Id,
    DateTimeOffset EndsAt,
    bool Ended,
    IReadOnlyList<CreatedOption> Options);

internal sealed class OpenResultsStream(HttpResponseMessage response, SseEventReader reader) : IDisposable
{
    public HttpResponseMessage Response { get; } = response;

    public SseEventReader Reader { get; } = reader;

    public void Dispose() => Response.Dispose();
}

internal sealed class SseEventReader(Stream stream)
{
    private readonly StringBuilder pending = new();

    public async Task<string> ReadDataAsync(TimeSpan timeout)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        while (true)
        {
            if (TryTakeData(out var data))
            {
                return data;
            }

            var buffer = new byte[2048];
            var read = await stream.ReadAsync(buffer.AsMemory(), timeoutSource.Token);
            if (read == 0)
            {
                throw new InvalidOperationException("Results stream ended before a results event.");
            }

            pending.Append(Encoding.UTF8.GetString(buffer, 0, read));
        }
    }

    private bool TryTakeData(out string data)
    {
        data = "";
        var text = pending.ToString();
        if (!TrySplitBlock(text, out var block, out var rest))
        {
            return false;
        }

        pending.Clear();
        pending.Append(rest);
        data = DataFrom(block);
        return true;
    }

    private static bool TrySplitBlock(string text, out string block, out string rest)
    {
        var crlf = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var lf = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (crlf >= 0 && (lf < 0 || crlf < lf))
        {
            block = text[..crlf];
            rest = text[(crlf + 4)..];
            return true;
        }

        if (lf >= 0)
        {
            block = text[..lf];
            rest = text[(lf + 2)..];
            return true;
        }

        block = "";
        rest = text;
        return false;
    }

    private static string DataFrom(string block)
    {
        string? eventName = null;
        var data = new StringBuilder();
        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Append(line["data:".Length..].TrimStart());
            }
        }

        if (eventName != "results" || data.Length == 0)
        {
            throw new InvalidOperationException($"Unexpected SSE block: {block}");
        }

        return data.ToString();
    }
}
