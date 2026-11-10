using System.Diagnostics;

namespace LivePoll.Integration.Load;

[Collection(LoadGroup.Name)]
public sealed class VoteEndpoint_P95_Under100ms_With1000Voters(LoadFixture stack)
{
    private const int voterCount = 1000;

    [Fact(DisplayName = "VoteEndpoint_P95_Under100ms_With1000Voters")]
    public async Task VoteEndpoint_At1000ConcurrentVoters_P95Under100ms()
    {
        var samples = new List<LoadSample>();
        var sampleErrors = new List<string>();
        CastVote[] results = [];
        var started = Stopwatch.StartNew();
        using var client = PublicSite.CreateClient(stack.Origin);
        using var sampling = new CancellationTokenSource();
        Task? sampler = null;
        string? organizer = null;
        Guid? pollId = null;
        try
        {
            await stack.EnsureWorkersAsync();
            organizer = await PublicSite.SignInAsync(client, "p95-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
            var poll = await PublicSite.CreatePollAsync(client, organizer, CancellationToken.None);
            pollId = poll.Id;
            var tokens = new string[voterCount];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, tokens.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 50 },
                async (index, cancellationToken) =>
                {
                    tokens[index] = await PublicSite.SignInAsync(
                        client,
                        $"p95-{index}-{Guid.NewGuid():N}",
                        cancellationToken);
                });

            sampler = SampleMetricsAsync(client, organizer, samples, sampleErrors, started, sampling.Token);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var votes = new Task<CastVote>[tokens.Length];
            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                votes[index] = VoteAfterGateAsync(client, gate, token, poll.Id, poll.FirstOptionId);
            }

            gate.SetResult();
            results = await Task.WhenAll(votes);
        }
        catch (Exception exception)
        {
            lock (sampleErrors)
            {
                sampleErrors.Add(exception.Message);
            }

            throw;
        }
        finally
        {
            await sampling.CancelAsync();
            if (sampler is not null)
            {
                try
                {
                    await sampler;
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (organizer is not null)
            {
                var followUp = Stopwatch.StartNew();
                while (followUp.Elapsed < TimeSpan.FromSeconds(15))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await RecordSampleAsync(client, organizer, samples, sampleErrors, started, CancellationToken.None);
                    if (followUp.Elapsed >= TimeSpan.FromSeconds(2) && LatestBacklog(samples) == 0)
                    {
                        break;
                    }
                }
            }
            var accepted = results.Where(vote => vote.Error is null).Select(vote => vote.Elapsed).OrderBy(elapsed => elapsed).ToArray();
            double? p95 = null;
            if (accepted.Length > 0)
            {
                var rank = (int)Math.Ceiling(accepted.Length * 0.95) - 1;
                p95 = accepted[rank].TotalMilliseconds;
            }

            var errors = results
                .Where(vote => vote.Error is not null)
                .Select(vote => new LoadError((int)vote.Status, vote.Error ?? ""))
                .Concat(sampleErrors.Select(detail => new LoadError(0, detail)))
                .ToArray();
            LoadReport.Write(
                stack.Root,
                new LoadSettings(voterCount, 0, 1000, pollId),
                samples.ToArray(),
                p95,
                errors);
        }

        Assert.All(results, vote => Assert.Equal(System.Net.HttpStatusCode.Accepted, vote.Status));
        var samplesForRank = results.Select(vote => vote.Elapsed).OrderBy(elapsed => elapsed).ToArray();
        var p95Rank = (int)Math.Ceiling(samplesForRank.Length * 0.95) - 1;
        Assert.True(samplesForRank[p95Rank] < TimeSpan.FromMilliseconds(100), $"Vote p95 was {samplesForRank[p95Rank]}.");
    }

    private static long LatestBacklog(List<LoadSample> samples)
    {
        lock (samples)
        {
            return samples.Count == 0 ? long.MaxValue : samples[^1].Backlog;
        }
    }

    private static async Task SampleMetricsAsync(
        HttpClient client,
        string token,
        List<LoadSample> samples,
        List<string> sampleErrors,
        Stopwatch started,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RecordSampleAsync(client, token, samples, sampleErrors, started, CancellationToken.None);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task RecordSampleAsync(
        HttpClient client,
        string token,
        List<LoadSample> samples,
        List<string> sampleErrors,
        Stopwatch started,
        CancellationToken cancellationToken)
    {
        try
        {
            var reading = await PublicSite.ReadMetricsAsync(client, token, cancellationToken);
            var elapsedMs = (int)started.Elapsed.TotalMilliseconds;
            lock (samples)
            {
                samples.Add(new LoadSample(elapsedMs, reading.VotesPerSecond, reading.Backlog));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (sampleErrors)
            {
                sampleErrors.Add(exception.Message);
            }
        }
    }

    private static async Task<CastVote> VoteAfterGateAsync(
        HttpClient client,
        TaskCompletionSource gate,
        string token,
        Guid pollId,
        Guid optionId)
    {
        await gate.Task;
        return await PublicSite.MeasureVoteAsync(client, token, pollId, optionId, CancellationToken.None);
    }
}