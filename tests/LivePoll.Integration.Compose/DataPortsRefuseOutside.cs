using System.Net.Sockets;

namespace LivePoll.Integration.Compose;

[Collection(ComposeGroup.Name)]
public sealed class DataPortsRefuseOutside(ComposeFixture stack)
{
    [Fact(DisplayName = "DataPortsRefuseOutside")]
    public async Task OutsideClientCannotConnect_ContainerOnTheNetworkCan()
    {
        var containers = await stack.ContainersAsync();
        var ruvio = Assert.Single(containers, container => container.Service == "ruvio");
        var postgres = Assert.Single(containers, container => container.Service == "postgres");
        Assert.Empty(ruvio.HostPorts);
        Assert.Empty(postgres.HostPorts);

        var ruvioIp = (await stack.InspectAsync(ruvio.Id, "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}")).Trim();
        var postgresIp = (await stack.InspectAsync(postgres.Id, "{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}")).Trim();
        Assert.False(await CanConnectAsync(ruvioIp, 6379));
        Assert.False(await CanConnectAsync(postgresIp, 5432));

        var network = (await stack.InspectAsync(
            postgres.Id,
            "{{range $name, $net := .NetworkSettings.Networks}}{{$name}}{{end}}")).Trim();
        var postgresProbe = await stack.RunAsync(
            "run",
            "--rm",
            "--network",
            network,
            "--entrypoint",
            "pg_isready",
            "postgres:18.6-alpine",
            "-h",
            "postgres",
            "-p",
            "5432",
            "-U",
            "livepoll");
        Assert.True(postgresProbe.ExitCode == 0, postgresProbe.Stderr + postgresProbe.Stdout);

        var ruvioProbe = await stack.RunAsync(
            "run",
            "--rm",
            "--network",
            network,
            "alpine:3.21",
            "nc",
            "-z",
            "-w",
            "3",
            "ruvio",
            "6379");
        Assert.True(ruvioProbe.ExitCode == 0, ruvioProbe.Stderr + ruvioProbe.Stdout);
    }

    private static async Task<bool> CanConnectAsync(string host, int port)
    {
        using var client = new TcpClient();
        try
        {
            var connect = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connect, Task.Delay(TimeSpan.FromSeconds(2)));
            if (completed != connect)
            {
                return false;
            }

            await connect;
            return client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}