namespace LivePoll.Build.Tests;

public sealed class TddCommitOrderTests
{
    [Fact]
    public void TestCommitPrecedesImplementation()
    {
        RepositoryScripts.RunNodeScript("tdd-commit-order.mjs");
        RepositoryScripts.RunNodeScript("tdd-commit-order.mjs", "--self-test");
    }
}