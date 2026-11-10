namespace LivePoll.Worker.Tests;

public sealed class BlockingRead_UsesOwnRuvioClient
{
    [Fact]
    public void RuvioVoteDeliverySource_ReadUsesSeparateBlockingClient()
    {
        var path = Path.Combine(
            RepoRoot.Find(),
            "src",
            "LivePoll.Worker",
            "Voting",
            "RuvioVoteDeliverySource.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("IRuvioClient commands", source, StringComparison.Ordinal);
        Assert.Contains("IRuvioClient blockingReads", source, StringComparison.Ordinal);
        Assert.Contains("blockingReads.GetStreamGroupUpdatesAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("commands.GetStreamGroupUpdatesAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_CreatesDedicatedClientForBlockingReads()
    {
        var path = Path.Combine(RepoRoot.Find(), "src", "LivePoll.Worker", "Program.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("blocking", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GetDefaultDatabaseAsync", source, StringComparison.Ordinal);
    }
}