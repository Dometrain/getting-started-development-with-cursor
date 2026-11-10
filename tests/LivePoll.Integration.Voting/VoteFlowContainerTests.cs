using LivePoll.Infrastructure.Ruvio;

using PollException = LivePoll.Application.ApplicationException;

namespace LivePoll.Integration.Voting;

[Collection(VoteFlowGroup.Name)]
public sealed class VoteFlowContainerTests(VoteFlowFixture stack)
{
    [Fact]
    public async Task PersistsThroughRealPostgresAndRuvio()
    {
        var ruvioImage = (await DockerInspect.ReadAsync(stack.RuvioContainerId, "{{.Config.Image}}")).Trim();
        var ruvioEnvironment = await DockerInspect.ReadAsync(
            stack.RuvioContainerId,
            "{{range .Config.Env}}{{println .}}{{end}}");
        var ruvioPorts = await DockerInspect.ReadAsync(stack.RuvioContainerId, "{{json .NetworkSettings.Ports}}");
        var postgresImage = (await DockerInspect.ReadAsync(stack.PostgresContainerId, "{{.Config.Image}}")).Trim();
        Assert.Contains("salihcantekin/ruvio", ComposeImages.Ruvio, StringComparison.Ordinal);
        Assert.Contains("salihcantekin/ruvio", ruvioImage, StringComparison.Ordinal);
        Assert.Contains($"RUVIO_REQUIREPASS={RuvioSession.Password}", ruvioEnvironment, StringComparison.Ordinal);
        Assert.Contains("RUVIO_DURABILITY=everysec", ruvioEnvironment, StringComparison.Ordinal);
        Assert.Contains("6379/tcp", ruvioPorts, StringComparison.Ordinal);
        Assert.Contains("postgres", postgresImage, StringComparison.Ordinal);

        var clock = MutableClock.UnixNow();
        var poll = await stack.CreatePollAsync(clock);
        var option = poll.Options[0];
        var accepted = await stack.Acceptance(clock).AcceptAsync(poll.Id, "ada", option.Id);
        Assert.True(accepted.AcceptedForStorage);

        var reader = await stack.OpenClientAsync();
        Assert.Equal(1L, await reader.ExistsAsync(RuvioKeys.Once(poll.Id, "ada")));

        await stack.StorePendingAsync(clock);

        var stored = Assert.Single(await stack.ReadVotesAsync(poll.Id));
        Assert.Equal(poll.Id, stored.PollId);
        Assert.Equal("ada", stored.UserId);
        Assert.Equal(option.Id, stored.OptionId);
    }
}
