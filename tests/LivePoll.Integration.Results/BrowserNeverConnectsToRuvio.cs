using System.Text;
using System.Text.RegularExpressions;

namespace LivePoll.Integration.Results;

[Collection(ResultsGroup.Name)]
public sealed class BrowserNeverConnectsToRuvio(ResultsFixture stack)
{
    [Fact(DisplayName = "BrowserNeverConnectsToRuvio")]
    public async Task WatchingResults_EveryConnectionIsThePublicSite()
    {
        await stack.EnsureReplicasAsync();
        var guard = new RecordingHandler();
        using var client = PublicSite.CreateClient(stack.Origin, guard);
        var token = await PublicSite.SignInAsync(client, "browser-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        var poll = await PublicSite.CreatePollAsync(client, token, CancellationToken.None);

        var page = await client.GetStringAsync("/", CancellationToken.None);
        var scripts = Regex.Matches(page, "src=\"(?<src>/assets/[^\"]+)\"");
        Assert.NotEmpty(scripts);
        var scriptText = new StringBuilder();
        foreach (Match script in scripts)
        {
            var source = await client.GetStringAsync(script.Groups["src"].Value, CancellationToken.None);
            scriptText.Append(source);
            Assert.DoesNotContain("://ruvio", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("://postgres", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(":6379", source, StringComparison.Ordinal);
            Assert.DoesNotContain(":5432", source, StringComparison.Ordinal);
        }

        Assert.Contains("/results/stream", scriptText.ToString(), StringComparison.Ordinal);

        await using var stream = await LiveResultsStream.OpenAsync(client, poll.Id, CancellationToken.None);
        _ = await stream.ReadEventAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        _ = await PublicSite.VoteAsync(client, token, poll.Id, poll.FirstOptionId, CancellationToken.None);
        _ = await stream.ReadUntilVotesAsync(poll.FirstOptionId, 1, TimeSpan.FromSeconds(3), CancellationToken.None);

        Assert.NotEmpty(guard.Uris);
        Assert.All(guard.Uris, uri =>
        {
            Assert.Equal(stack.Origin.Host, uri.Host);
            Assert.Equal(stack.Origin.Port, uri.Port);
            Assert.NotEqual("ruvio", uri.Host);
            Assert.DoesNotContain("6379", uri.Authority, StringComparison.Ordinal);
            Assert.DoesNotContain("5432", uri.Authority, StringComparison.Ordinal);
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