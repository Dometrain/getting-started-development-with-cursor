using LivePoll.Simulator;

namespace LivePoll.Simulator.Tests;

public sealed class SimulatorPlanTests
{
    private static readonly Guid optionA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid optionB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid optionC = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void CountRampDurationAndSplitShapeTheVotes()
    {
        var baseline = VotePlan.Create(new SimulationSettings(
            VoterCount: 10,
            RampUpSeconds: 20,
            DurationSeconds: 60,
            VoteSplit: [new VoteSplit(optionA, 70), new VoteSplit(optionB, 30)],
            Seed: 42));

        var moreVoters = VotePlan.Create(new SimulationSettings(
            VoterCount: 20,
            RampUpSeconds: 20,
            DurationSeconds: 60,
            VoteSplit: [new VoteSplit(optionA, 70), new VoteSplit(optionB, 30)],
            Seed: 42));

        var slowerRamp = VotePlan.Create(new SimulationSettings(
            VoterCount: 10,
            RampUpSeconds: 40,
            DurationSeconds: 60,
            VoteSplit: [new VoteSplit(optionA, 70), new VoteSplit(optionB, 30)],
            Seed: 42));

        var longerDuration = VotePlan.Create(new SimulationSettings(
            VoterCount: 10,
            RampUpSeconds: 20,
            DurationSeconds: 120,
            VoteSplit: [new VoteSplit(optionA, 70), new VoteSplit(optionB, 30)],
            Seed: 42));

        var differentSplit = VotePlan.Create(new SimulationSettings(
            VoterCount: 10,
            RampUpSeconds: 20,
            DurationSeconds: 60,
            VoteSplit: [new VoteSplit(optionA, 30), new VoteSplit(optionB, 70)],
            Seed: 42));

        Assert.Equal(10, baseline.Votes.Count);
        Assert.Equal(20, moreVoters.Votes.Count);
        Assert.NotEqual(
            baseline.Votes.Select(vote => vote.Username).ToArray(),
            moreVoters.Votes.Select(vote => vote.Username).ToArray());

        Assert.NotEqual(
            baseline.Votes.Select(vote => vote.JoinAfter).ToArray(),
            slowerRamp.Votes.Select(vote => vote.JoinAfter).ToArray());
        Assert.True(slowerRamp.Votes.Max(vote => vote.JoinAfter) > baseline.Votes.Max(vote => vote.JoinAfter));

        Assert.Equal(60, baseline.DurationSeconds);
        Assert.Equal(120, longerDuration.DurationSeconds);
        Assert.NotEqual(baseline.DurationSeconds, longerDuration.DurationSeconds);

        var baselineCounts = CountByOption(baseline);
        var splitCounts = CountByOption(differentSplit);
        Assert.NotEqual(baselineCounts[optionA], splitCounts[optionA]);
        Assert.NotEqual(baselineCounts[optionB], splitCounts[optionB]);

        for (var index = 0; index < baseline.Votes.Count; index++)
        {
            var vote = baseline.Votes[index];
            Assert.Equal(VotePlan.JoinAfter(index, baseline.Settings.VoterCount, baseline.Settings.RampUpSeconds), vote.JoinAfter);
            Assert.Equal(VotePlan.Username(baseline.Settings.Seed, index), vote.Username);
            Assert.Equal(
                VotePlan.ChooseOption(baseline.Settings.VoteSplit, baseline.Settings.Seed, index, baseline.Settings.VoterCount),
                vote.OptionId);
        }
    }

    [Fact]
    public void SameSettingsAndSeedProduceTheSameVotes()
    {
        var settings = new SimulationSettings(
            VoterCount: 100,
            RampUpSeconds: 30,
            DurationSeconds: 90,
            VoteSplit: [new VoteSplit(optionA, 40), new VoteSplit(optionB, 35), new VoteSplit(optionC, 25)],
            Seed: 7);

        var first = VotePlan.Create(settings);
        var second = VotePlan.Create(settings);
        var otherSeed = VotePlan.Create(settings with { Seed = 8 });

        Assert.Equal(first.Votes, second.Votes);
        Assert.Equal(first.DurationSeconds, second.DurationSeconds);

        Assert.NotEqual(
            first.Votes.Select(vote => vote.OptionId).ToArray(),
            otherSeed.Votes.Select(vote => vote.OptionId).ToArray());
        Assert.NotEqual(
            first.Votes.Select(vote => vote.Username).ToArray(),
            otherSeed.Votes.Select(vote => vote.Username).ToArray());
    }

    [Fact]
    public void Simulator_HasNoRuvioOrPostgresClientReferences()
    {
        var referenced = typeof(VotePlan).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name!)
            .ToArray();

        Assert.DoesNotContain(referenced, name => name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.Contains("Ruvio", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, name => name.Contains("StackExchange.Redis", StringComparison.OrdinalIgnoreCase));

        var projectText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "LivePoll.Simulator", "LivePoll.Simulator.csproj"));
        Assert.DoesNotContain("Npgsql", projectText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ruvio", projectText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EntityFramework", projectText, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<Guid, int> CountByOption(VotePlan plan) =>
        plan.Votes.GroupBy(vote => vote.OptionId).ToDictionary(group => group.Key, group => group.Count());

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LivePoll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate LivePoll.slnx from the test output directory.");
    }
}