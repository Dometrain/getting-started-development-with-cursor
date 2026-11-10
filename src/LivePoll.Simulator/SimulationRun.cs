namespace LivePoll.Simulator;

public sealed class SimulationRun : IDisposable
{
    private const string runningStatus = "running";
    private const string stoppedStatus = "stopped";
    private const string completedStatus = "completed";

    private readonly HttpClient client;
    private readonly Guid pollId;
    private readonly VotePlan plan;
    private readonly TimeProvider time;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object statusGate = new();
    private string status = runningStatus;
    private Task finished = Task.CompletedTask;

    private SimulationRun(HttpClient client, Guid pollId, VotePlan plan, TimeProvider time)
    {
        this.client = client;
        this.pollId = pollId;
        this.plan = plan;
        this.time = time;
    }

    public string Status
    {
        get
        {
            lock (statusGate)
            {
                return status;
            }
        }
    }

    public Task Finished => finished;

    public static SimulationRun NotStarted(HttpClient client, VotePlan plan)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(plan);
        return new SimulationRun(client, Guid.Empty, plan, TimeProvider.System);
    }

    public static SimulationRun Start(HttpClient client, Guid pollId, VotePlan plan, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(time);

        var run = new SimulationRun(client, pollId, plan, time);
        run.finished = run.ExecuteAsync();
        return run;
    }

    public void Stop()
    {
        if (!TryTransition(stoppedStatus))
        {
            return;
        }

        CancelLifetime();
    }

    public void Dispose() => Stop();

    private async Task ExecuteAsync()
    {
        var voters = new Task[plan.Votes.Count];
        for (var index = 0; index < plan.Votes.Count; index++)
        {
            voters[index] = CastWhenDueAsync(plan.Votes[index], lifetime.Token);
        }

        try
        {
            await WaitForDurationAsync();
            await Task.WhenAll(voters);
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    private async Task WaitForDurationAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(plan.DurationSeconds), time, lifetime.Token);
            if (TryTransition(completedStatus))
            {
                CancelLifetime();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CastWhenDueAsync(PlannedVote vote, CancellationToken cancellationToken)
    {
        try
        {
            if (vote.JoinAfter > TimeSpan.Zero)
            {
                await Task.Delay(vote.JoinAfter, time, cancellationToken);
            }

            await VirtualVoter.CastAsync(client, pollId, vote, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool TryTransition(string next)
    {
        lock (statusGate)
        {
            if (status != runningStatus)
            {
                return false;
            }

            status = next;
            return true;
        }
    }

    private void CancelLifetime()
    {
        try
        {
            lifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}