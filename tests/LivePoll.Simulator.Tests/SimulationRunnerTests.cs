using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

using LivePoll.Simulator;

namespace LivePoll.Simulator.Tests;

public sealed class SimulationRunnerTests
{
    private static readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private static readonly Guid optionA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid optionB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Uri publicApi = new("http://public.test/");

    [Fact]
    public void Create_WithPublicBaseAddress_UsesOneSocketsHttpClientWithConnectionLimit()
    {
        using var runner = new SimulationRunner(publicApi);

        var handler = Assert.IsType<SocketsHttpHandler>(HandlerOf(runner.SharedClient));
        Assert.Same(handler, runner.ConnectionHandler);
        Assert.Equal(VoterTransport.ConnectionLimit, handler.MaxConnectionsPerServer);
        Assert.InRange(handler.MaxConnectionsPerServer, 1, 10_000);
        Assert.NotEqual(int.MaxValue, handler.MaxConnectionsPerServer);
        Assert.Equal(publicApi, runner.SharedClient.BaseAddress);
    }

    [Fact]
    public async Task Start_WhenCalled_ReportsRunningAsync()
    {
        await using var scope = await RunnerScope.StartAsync(blockRequests: true, rampUpSeconds: 0, durationSeconds: 30);

        Assert.Equal("running", scope.Run.Status);
        Assert.False(scope.Run.Finished.IsCompleted);
    }

    [Fact]
    public async Task Stop_WhileRunning_ReportsStoppedAsync()
    {
        await using var scope = await RunnerScope.StartAsync(blockRequests: true, rampUpSeconds: 0, durationSeconds: 30);

        scope.Run.Stop();

        Assert.Equal("stopped", scope.Run.Status);
        scope.Time.Advance(TimeSpan.FromSeconds(scope.Plan.DurationSeconds));
        await scope.Run.Finished.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("stopped", scope.Run.Status);
    }

    [Fact]
    public async Task Duration_WhenElapsedWithoutStop_ReportsCompletedAsync()
    {
        await using var scope = await RunnerScope.StartAsync(blockRequests: false, rampUpSeconds: 0, durationSeconds: 30);

        await WaitUntilAsync(() => scope.Handler.VoteCount == scope.Plan.Votes.Count, TimeSpan.FromSeconds(2));
        Assert.Equal("running", scope.Run.Status);

        scope.Time.Advance(TimeSpan.FromSeconds(scope.Plan.DurationSeconds));
        await scope.Run.Finished.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("completed", scope.Run.Status);
    }

    [Fact]
    public async Task Run_AgainstFakeHandler_SignsInAndVotesOnlyOnPublicPathsAsync()
    {
        await using var scope = await RunnerScope.StartAsync(blockRequests: false, rampUpSeconds: 0, durationSeconds: 30, voterCount: 6);

        await WaitUntilAsync(() => scope.Handler.Arrivals == scope.Plan.Votes.Count * 2, TimeSpan.FromSeconds(2));
        var requests = scope.Handler.Snapshot();
        var votePath = $"/polls/{pollId}/votes";

        Assert.Equal(scope.Plan.Votes.Count * 2, requests.Length);
        Assert.All(requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("public.test", request.Host);
            Assert.True(
                request.Path == "/auth/dev-token" || request.Path == votePath,
                request.Path);
        });

        foreach (var vote in scope.Plan.Votes)
        {
            var signIn = Assert.Single(
                requests,
                request => request.Path == "/auth/dev-token" && ReadUsername(request.Body) == vote.Username);
            Assert.Null(signIn.Authorization);

            var cast = Assert.Single(
                requests,
                request => request.Path == votePath && request.Authorization == $"Bearer tok-{vote.Username}");
            Assert.Equal(vote.OptionId, ReadOptionId(cast.Body));
        }
    }

    [Fact]
    public async Task Vote_WhenServerReturns429_IsNotRetriedAsync()
    {
        await using var scope = await RunnerScope.StartAsync(
            blockRequests: false,
            rampUpSeconds: 0,
            durationSeconds: 30,
            voterCount: 4,
            rateLimitVotes: true);

        await WaitUntilAsync(() => scope.Handler.VoteCount == scope.Plan.Votes.Count, TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => scope.Handler.InFlight == 0, TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        var votes = scope.Handler.Snapshot().Where(request => request.Path == $"/polls/{pollId}/votes").ToArray();
        Assert.Equal(scope.Plan.Votes.Count, votes.Length);
        Assert.Equal(scope.Plan.Votes.Count, votes.Select(request => request.Authorization).Distinct().Count());
    }

    [Fact]
    public async Task RampUp_WhenVotersJoin_OverlapsOnTheSharedClientAsync()
    {
        const int voters = 4;
        await using var scope = await RunnerScope.StartAsync(
            blockRequests: true,
            rampUpSeconds: voters,
            durationSeconds: 30,
            voterCount: voters);

        Assert.Same(scope.Client, scope.Runner.SharedClient);
        await WaitUntilAsync(() => scope.Handler.Arrivals == 1, TimeSpan.FromSeconds(2));

        for (var expected = 2; expected <= voters; expected++)
        {
            scope.Time.Advance(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => scope.Handler.Arrivals == expected, TimeSpan.FromSeconds(2));
        }

        Assert.Equal(voters, scope.Handler.MaxInFlight);
        Assert.True(scope.Handler.MaxInFlight > 1);
        Assert.Equal("running", scope.Run.Status);
        Assert.All(scope.Handler.Snapshot(), request => Assert.Equal("/auth/dev-token", request.Path));
    }

    private static HttpMessageHandler HandlerOf(HttpClient client)
    {
        var field = typeof(HttpMessageInvoker).GetField(
            "_handler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<HttpMessageHandler>(field.GetValue(client));
    }

    private static string ReadUsername(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("username").GetString()!;
    }

    private static Guid ReadOptionId(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("optionId").GetGuid();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - started > timeout)
            {
                throw new TimeoutException("Timed out waiting for the virtual voters.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class RunnerScope : IAsyncDisposable
    {
        private RunnerScope(
            RecordingHandler handler,
            HttpClient client,
            SimulationRunner runner,
            ManualTimeProvider time,
            VotePlan plan,
            SimulationRun run)
        {
            Handler = handler;
            Client = client;
            Runner = runner;
            Time = time;
            Plan = plan;
            Run = run;
        }

        public RecordingHandler Handler { get; }

        public HttpClient Client { get; }

        public SimulationRunner Runner { get; }

        public ManualTimeProvider Time { get; }

        public VotePlan Plan { get; }

        public SimulationRun Run { get; }

        public static Task<RunnerScope> StartAsync(
            bool blockRequests,
            int rampUpSeconds,
            int durationSeconds,
            int voterCount = 4,
            bool rateLimitVotes = false)
        {
            var handler = new RecordingHandler(blockRequests, rateLimitVotes);
            var client = new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = publicApi,
            };
            var runner = new SimulationRunner(client);
            var time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
            var plan = VotePlan.Create(new SimulationSettings(
                voterCount,
                rampUpSeconds,
                durationSeconds,
                [new VoteSplit(optionA, 50), new VoteSplit(optionB, 50)],
                Seed: 42));
            var run = runner.Start(pollId, plan, time);
            return Task.FromResult(new RunnerScope(handler, client, runner, time, plan, run));
        }

        public async ValueTask DisposeAsync()
        {
            Handler.Release();
            Run.Stop();
            await Run.Finished.WaitAsync(TimeSpan.FromSeconds(2));
            Runner.Dispose();
            Client.Dispose();
            Handler.Dispose();
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly bool blockRequests;
        private readonly bool rateLimitVotes;
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object gate = new();
        private readonly List<CapturedRequest> records = [];
        private int inFlight;
        private int maxInFlight;

        public RecordingHandler(bool blockRequests, bool rateLimitVotes)
        {
            this.blockRequests = blockRequests;
            this.rateLimitVotes = rateLimitVotes;
            if (!blockRequests)
            {
                released.TrySetResult();
            }
        }

        public int Arrivals
        {
            get
            {
                lock (gate)
                {
                    return records.Count;
                }
            }
        }

        public int InFlight => Volatile.Read(ref inFlight);

        public int MaxInFlight
        {
            get
            {
                lock (gate)
                {
                    return maxInFlight;
                }
            }
        }

        public int VoteCount
        {
            get
            {
                lock (gate)
                {
                    return records.Count(record => record.Path.EndsWith("/votes", StringComparison.Ordinal));
                }
            }
        }

        public void Release() => released.TrySetResult();

        public CapturedRequest[] Snapshot()
        {
            lock (gate)
            {
                return records.ToArray();
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref inFlight);
            lock (gate)
            {
                if (current > maxInFlight)
                {
                    maxInFlight = current;
                }
            }

            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri?.Host ?? "",
                request.RequestUri?.AbsolutePath ?? "",
                body,
                request.Headers.Authorization?.ToString());

            lock (gate)
            {
                records.Add(captured);
            }

            try
            {
                if (blockRequests)
                {
                    await released.Task.WaitAsync(cancellationToken);
                }

                return ResponseFor(captured);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        private HttpResponseMessage ResponseFor(CapturedRequest request)
        {
            if (request.Path == "/auth/dev-token")
            {
                var username = ReadUsername(request.Body);
                var json = $$"""{"token":"tok-{{username}}","username":"{{username}}"}""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            if (rateLimitVotes)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{"type":"/problems/rate-limited","title":"Too many requests","status":429,"detail":"limited"}""",
                        Encoding.UTF8,
                        "application/problem+json"),
                };
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return limited;
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                released.TrySetResult();
            }

            base.Dispose(disposing);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string Host,
        string Path,
        string Body,
        string? Authorization);
}