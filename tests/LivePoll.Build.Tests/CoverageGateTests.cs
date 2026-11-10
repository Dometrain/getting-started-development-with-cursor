namespace LivePoll.Build.Tests;

public sealed class CoverageGateTests
{
    [Fact]
    public void DomainAndApplicationLineCoverageAtLeast80()
    {
        RepositoryScripts.RunNodeScript("test-coverage.mjs");
        RepositoryScripts.RunNodeScript("test-coverage.mjs", "--self-test");
    }
}