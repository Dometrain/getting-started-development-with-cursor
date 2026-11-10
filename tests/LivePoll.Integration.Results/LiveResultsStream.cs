using System.Net;
using System.Text;
using System.Text.Json;

namespace LivePoll.Integration.Results;

internal sealed class ResultsStreamEndedException : Exception;

internal sealed class LiveResultsStream : IAsyncDisposable
{
    private readonly HttpResponseMessage response;
    private readonly SseReader reader;
    private bool ended;

    private LiveResultsStream(HttpResponseMessage response, Stream body, string? destination)
    {
        this.response = response;
        reader = new SseReader(body);
        Destination = destination;
        ContentType = response.Content.Headers.ContentType?.MediaType;
        ContentLength = response.Content.Headers.ContentLength;
    }

    public string? Destination { get; }

    public HttpStatusCode StatusCode => response.StatusCode;

    public string? ContentType { get; }

    public long? ContentLength { get; }

    public bool StillOpen => !ended;

    public static async Task<LiveResultsStream> OpenAsync(
        HttpClient client,
        Guid pollId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/polls/{pollId}/results/stream");
        request.Headers.Accept.ParseAdd("text/event-stream");
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new InvalidOperationException($"Results stream returned {(int)response.StatusCode}: {text}");
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new LiveResultsStream(response, body, PublicSite.Destination(response));
    }

    public async Task<JsonElement> ReadEventAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var data = await reader.ReadResultsDataAsync(timeout, cancellationToken);
            using var document = JsonDocument.Parse(data);
            return document.RootElement.Clone();
        }
        catch (ResultsStreamEndedException)
        {
            ended = true;
            throw;
        }
    }

    public async Task<JsonElement> ReadUntilVotesAsync(
        Guid optionId,
        long minimum,
        TimeSpan budget,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + budget;
        JsonElement last = default;
        while (DateTime.UtcNow < deadline)
        {
            last = await ReadEventAsync(deadline - DateTime.UtcNow, cancellationToken);
            if (Votes(last, optionId) >= minimum)
            {
                return last;
            }
        }

        throw new TimeoutException($"Option {optionId} stayed below {minimum}. Last event: {last}");
    }

    public static long Votes(JsonElement snapshot, Guid optionId)
    {
        if (!snapshot.TryGetProperty("options", out var options))
        {
            return 0;
        }

        foreach (var option in options.EnumerateArray())
        {
            if (option.GetProperty("optionId").GetGuid() == optionId)
            {
                return option.GetProperty("votes").GetInt64();
            }
        }

        return 0;
    }

    public ValueTask DisposeAsync()
    {
        response.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class SseReader(Stream stream)
    {
        private readonly StringBuilder pending = new();

        public async Task<string> ReadResultsDataAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout > TimeSpan.Zero)
            {
                timeoutSource.CancelAfter(timeout);
            }
            else
            {
                timeoutSource.Cancel();
            }

            while (true)
            {
                if (TryTakeResultsData(out var data))
                {
                    return data;
                }

                var buffer = new byte[4096];
                int read;
                try
                {
                    read = await stream.ReadAsync(buffer.AsMemory(), timeoutSource.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("Results event did not arrive in time.");
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException)
                {
                    throw new ResultsStreamEndedException();
                }

                if (read == 0)
                {
                    throw new ResultsStreamEndedException();
                }

                pending.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }
        }

        private bool TryTakeResultsData(out string data)
        {
            data = "";
            while (TrySplitBlock(pending.ToString(), out var block, out var rest))
            {
                pending.Clear();
                pending.Append(rest);
                if (TryReadResults(block, out data))
                {
                    return true;
                }
            }

            return false;
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

        private static bool TryReadResults(string block, out string data)
        {
            string? eventName = null;
            var payload = new StringBuilder();
            foreach (var rawLine in block.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    eventName = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    payload.Append(line["data:".Length..].TrimStart());
                }
            }

            if (!string.Equals(eventName, "results", StringComparison.Ordinal) || payload.Length == 0)
            {
                data = "";
                return false;
            }

            data = payload.ToString();
            return true;
        }
    }
}