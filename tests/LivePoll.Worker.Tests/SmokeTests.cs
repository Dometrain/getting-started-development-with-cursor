namespace LivePoll.Worker.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Worker_Type_Exists()
    {
        Assert.Equal("Worker", nameof(LivePoll.Worker.Worker));
    }
}