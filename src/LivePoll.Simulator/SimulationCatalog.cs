using System.Collections.Concurrent;

namespace LivePoll.Simulator;

public sealed class SimulationCatalog
{
    private readonly ISimulationRunner runner;
    private readonly ConcurrentDictionary<Guid, RunningSimulation> simulations = new();

    public SimulationCatalog(ISimulationRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        this.runner = runner;
    }

    public RunningSimulation Start(StartSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.VoteSplit);

        var settings = new SimulationSettings(
            request.VoterCount,
            request.RampUpSeconds,
            request.DurationSeconds,
            request.VoteSplit,
            request.Seed);
        var plan = VotePlan.Create(settings);
        var run = runner.Start(request.PollId, plan);
        var simulation = new RunningSimulation(Guid.NewGuid(), request, run, DateTimeOffset.UtcNow);
        simulations[simulation.Id] = simulation;
        return simulation;
    }

    public int StoredCount => simulations.Count;

    public RunningSimulation? Find(Guid id) =>
        simulations.TryGetValue(id, out var simulation) ? simulation : null;

    public RunningSimulation? Stop(Guid id)
    {
        var simulation = Find(id);
        simulation?.Stop();
        return simulation;
    }
}

public sealed class RunningSimulation
{
    private readonly SimulationRun run;
    private readonly StartSimulationRequest request;
    private readonly DateTimeOffset startedAt;
    private DateTimeOffset? stoppedAt;

    public RunningSimulation(
        Guid id,
        StartSimulationRequest request,
        SimulationRun run,
        DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(run);
        Id = id;
        this.request = request;
        this.run = run;
        this.startedAt = startedAt;
    }

    public Guid Id { get; }

    public void Stop()
    {
        run.Stop();
        stoppedAt ??= DateTimeOffset.UtcNow;
    }

    public SimulationResponse ToResponse()
    {
        var status = run.Status;
        DateTimeOffset? endedAt = status == "running"
            ? null
            : stoppedAt ?? startedAt.AddSeconds(request.DurationSeconds);

        return new SimulationResponse(
            Id,
            status,
            request.PollId,
            request.VoterCount,
            request.RampUpSeconds,
            request.DurationSeconds,
            request.VoteSplit ?? [],
            request.Seed,
            startedAt,
            endedAt);
    }
}