using Ruvio.Client;

namespace LivePoll.Infrastructure.Tests;

[Collection(RuvioContainerGroup.Name)]
public sealed class Fixture_StartsRuvioWithPasswordAndEverysec(RuvioFixture fixture)
{
    [Fact]
    public async Task Container_UsesProjectImagePasswordAndEverysec()
    {
        Assert.Contains("salihcantekin/ruvio", RuvioContainerSession.Image, StringComparison.Ordinal);

        var image = (await RuvioContainerSession.InspectAsync(fixture.ContainerId, "{{.Config.Image}}")).Trim();
        Assert.Contains("salihcantekin/ruvio", image, StringComparison.Ordinal);

        var environment = await RuvioContainerSession.InspectAsync(
            fixture.ContainerId,
            "{{range .Config.Env}}{{println .}}{{end}}");
        var variables = environment.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains($"RUVIO_REQUIREPASS={RuvioContainerSession.Password}", variables);
        Assert.Contains("RUVIO_DURABILITY=everysec", variables);

        Assert.Equal("PONG", await fixture.Client.PingAsync());

        await Assert.ThrowsAnyAsync<RuvioException>(async () =>
        {
            await using var anonymous = await RuvioConnection.ConnectAsync(new RuvioClientOptions
            {
                Host = "127.0.0.1",
                Port = fixture.Port,
            });
            var database = await anonymous.GetDefaultDatabaseAsync();
            await database.PingAsync();
        });
    }
}
