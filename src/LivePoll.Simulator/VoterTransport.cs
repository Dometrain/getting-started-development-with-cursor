namespace LivePoll.Simulator;

public sealed class VoterTransport : IDisposable
{
    public const int ConnectionLimit = 1000;

    private readonly HttpClient client;

    private VoterTransport(SocketsHttpHandler handler, HttpClient client)
    {
        Handler = handler;
        this.client = client;
    }

    public SocketsHttpHandler Handler { get; }

    public HttpClient Client => client;

    public static VoterTransport Create(Uri publicBaseAddress)
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = ConnectionLimit,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false,
            AllowAutoRedirect = false,
        };

        try
        {
            var created = new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = publicBaseAddress,
            };
            return new VoterTransport(handler, created);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    public void Dispose() => client.Dispose();
}