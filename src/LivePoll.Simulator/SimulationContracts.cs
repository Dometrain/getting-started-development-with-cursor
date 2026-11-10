namespace LivePoll.Simulator;

public sealed record StartSimulationRequest(
    Guid PollId,
    int VoterCount,
    int RampUpSeconds,
    int DurationSeconds,
    IReadOnlyList<VoteSplit>? VoteSplit,
    int Seed);

public sealed record SimulationResponse(
    Guid Id,
    string Status,
    Guid PollId,
    int VoterCount,
    int RampUpSeconds,
    int DurationSeconds,
    IReadOnlyList<VoteSplit> VoteSplit,
    int Seed,
    DateTimeOffset StartedAt,
    DateTimeOffset? StoppedAt);