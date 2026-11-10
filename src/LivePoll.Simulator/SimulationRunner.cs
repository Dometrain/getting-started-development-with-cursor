namespace LivePoll.Simulator;

public interface ISimulationRunner
{
    SimulationRun Start(Guid pollId, VotePlan plan, TimeProvider? time = null);
}

public sealed class SimulationRunner : ISimulationRunner, IDisposable
{
    private readonly HttpClient sharedClient;
    private readonly VoterTransport? transport;

    public SimulationRunner(Uri publicBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(publicBaseAddress);
        if (!publicBaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("The public API address must be absolute.", nameof(publicBaseAddress));
        }

        transport = VoterTransport.Create(publicBaseAddress);
        sharedClient = transport.Client;
    }

    public SimulationRunner(HttpClient sharedClient)
    {
        ArgumentNullException.ThrowIfNull(sharedClient);
        this.sharedClient = sharedClient;
    }

    public HttpClient SharedClient => sharedClient;

    public SocketsHttpHandler? ConnectionHandler => transport?.Handler;

    public SimulationRun Start(Guid pollId, VotePlan plan, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return SimulationRun.Start(sharedClient, pollId, plan, time ?? TimeProvider.System);
    }

    public void Dispose() => transport?.Dispose();
}