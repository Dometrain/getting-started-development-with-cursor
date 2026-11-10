using System.Net;
using System.Text.RegularExpressions;

namespace LivePoll.Integration.Compose;

[Collection(ComposeGroup.Name)]
public sealed class Browser_TalksOnlyToNginx(ComposeFixture stack)
{
    [Fact(DisplayName = "Browser_TalksOnlyToNginx")]
    public async Task SignInVotingResultsAndMetrics_GoToNginx()
    {
        var guard = new RecordingHandler();
        using var client = new LivePollClient(new HttpClient(guard)
        {
            BaseAddress = stack.Origin,
            Timeout = TimeSpan.FromSeconds(30),
        });
        await client.WaitUntilReadyAsync(CancellationToken.None);

        var token = await client.SignInAsync("ada-browser", CancellationToken.None);
        var poll = await client.CreatePollAsync(token, CancellationToken.None);
        await client.VoteAsync(token, poll.Id, poll.FirstOptionId, CancellationToken.None);
        await client.WaitForVotesAsync(poll.Id, poll.FirstOptionId, 1, CancellationToken.None);
        using var metrics = await client.GetMetricsAsync(token, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);

        var html = await client.GetStringAsync("/", CancellationToken.None);
        var scriptPath = Regex.Match(html, "src=\"(?<src>/assets/[^\"]+)\"").Groups["src"].Value;
        Assert.False(string.IsNullOrEmpty(scriptPath));
        var script = await client.GetStringAsync(scriptPath, CancellationToken.None);
        Assert.Contains("/auth/dev-token", script, StringComparison.Ordinal);
        Assert.Contains("/metrics", script, StringComparison.Ordinal);
        Assert.DoesNotContain("://ruvio", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("://postgres", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":6379", script, StringComparison.Ordinal);
        Assert.DoesNotContain(":5432", script, StringComparison.Ordinal);

        Assert.NotEmpty(guard.Uris);
        Assert.All(guard.Uris, uri =>
        {
            Assert.Equal(stack.Origin.Host, uri.Host);
            Assert.Equal(stack.Origin.Port, uri.Port);
        });
    }

    private sealed class RecordingHandler : DelegatingHandler
    {
        public List<Uri> Uris { get; } = [];

        public RecordingHandler()
            : base(new HttpClientHandler())
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
            {
                Uris.Add(request.RequestUri);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}