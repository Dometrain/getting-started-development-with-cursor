namespace LivePoll.Worker.Tests;

public sealed class Worker_HasNoSignInOrPublicVoteEndpoint
{
    [Fact]
    public void Program_DoesNotExposeSignInOrVoteRoutes()
    {
        var programPath = Path.Combine(RepoRoot.Find(), "src", "LivePoll.Worker", "Program.cs");
        var source = File.ReadAllText(programPath);

        Assert.DoesNotContain("MapPost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/auth/token", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/votes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WebApplication", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_IsWorkerHostNotWebHost()
    {
        var csprojPath = Path.Combine(RepoRoot.Find(), "src", "LivePoll.Worker", "LivePoll.Worker.csproj");
        var csproj = File.ReadAllText(csprojPath);

        Assert.Contains("Microsoft.NET.Sdk.Worker", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", csproj, StringComparison.Ordinal);
    }
}