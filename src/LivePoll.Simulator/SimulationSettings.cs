namespace LivePoll.Simulator;

public sealed record SimulationSettings(
    int VoterCount,
    int RampUpSeconds,
    int DurationSeconds,
    IReadOnlyList<VoteSplit> VoteSplit,
    int Seed);