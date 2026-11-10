namespace LivePoll.Simulator;

public sealed class VotePlan
{
    private const int maximumVoterCount = 2000;
    private const int maximumDurationSeconds = 600;

    private VotePlan(SimulationSettings settings, IReadOnlyList<PlannedVote> votes)
    {
        Settings = settings;
        Votes = votes;
        DurationSeconds = settings.DurationSeconds;
    }

    public SimulationSettings Settings { get; }

    public int DurationSeconds { get; }

    public IReadOnlyList<PlannedVote> Votes { get; }

    public static VotePlan Create(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.VoteSplit);

        if (settings.VoterCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Voter count must be at least 1.");
        }

        if (settings.VoterCount > maximumVoterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Voter count must be at most 2000.");
        }

        if (settings.RampUpSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Ramp-up seconds cannot be negative.");
        }

        if (settings.DurationSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Duration seconds must be at least 1.");
        }

        if (settings.DurationSeconds > maximumDurationSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Duration seconds must be at most 600.");
        }

        var optionAssignments = BuildOptionAssignments(settings.VoteSplit, settings.Seed, settings.VoterCount);
        var votes = new PlannedVote[settings.VoterCount];
        for (var index = 0; index < settings.VoterCount; index++)
        {
            votes[index] = new PlannedVote(
                index,
                JoinAfter(index, settings.VoterCount, settings.RampUpSeconds),
                Username(settings.Seed, index),
                optionAssignments[index]);
        }

        return new VotePlan(settings, votes);
    }

    public static TimeSpan JoinAfter(int index, int voterCount, int rampUpSeconds)
    {
        if (index < 0 || index >= voterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (voterCount <= 1 || rampUpSeconds == 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(index * (double)rampUpSeconds / voterCount);
    }

    public static string Username(int seed, int index) =>
        $"sim-{seed}-{index}";

    public static Guid ChooseOption(
        IReadOnlyList<VoteSplit> voteSplit,
        int seed,
        int index,
        int voterCount)
    {
        ArgumentNullException.ThrowIfNull(voteSplit);

        if (voteSplit.Count == 0)
        {
            throw new ArgumentException("Vote split must contain at least one option.", nameof(voteSplit));
        }

        if (index < 0 || index >= voterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return BuildOptionAssignments(voteSplit, seed, voterCount)[index];
    }

    private static Guid[] BuildOptionAssignments(
        IReadOnlyList<VoteSplit> voteSplit,
        int seed,
        int voterCount)
    {
        var assignments = new Guid[voterCount];
        var filled = 0;

        for (var splitIndex = 0; splitIndex < voteSplit.Count; splitIndex++)
        {
            var entry = voteSplit[splitIndex];
            var count = splitIndex == voteSplit.Count - 1
                ? voterCount - filled
                : voterCount * entry.Percent / 100;

            for (var i = 0; i < count; i++)
            {
                assignments[filled + i] = entry.OptionId;
            }

            filled += count;
        }

        Shuffle(assignments, seed);
        return assignments;
    }

    private static void Shuffle(Guid[] values, int seed)
    {
        var random = new Random(seed);
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}