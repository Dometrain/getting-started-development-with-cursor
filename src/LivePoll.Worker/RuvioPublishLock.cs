using Ruvio.Client;

namespace LivePoll.Worker;

public sealed class RuvioPublishLock(IRuvioClient client) : IPublishLock
{
    public const string Key = "lease:results-publisher";

    public async Task<IPublishLockHold?> TryAcquireAsync(TimeSpan lease, CancellationToken cancellationToken)
    {
        var acquired = await client.TryAcquireLeaseAsync(Key, lease, cancellationToken);
        if (acquired is null)
        {
            return null;
        }

        return new RenewingHold(acquired, lease);
    }

    private sealed class RenewingHold : IPublishLockHold
    {
        private readonly RuvioLease lease;
        private readonly CancellationTokenSource renewalStopped = new();
        private readonly Task renewal;
        private int held = 1;

        public RenewingHold(RuvioLease lease, TimeSpan duration)
        {
            this.lease = lease;
            FencingToken = lease.Token;
            renewal = RenewAsync(duration, renewalStopped.Token);
        }

        public bool IsHeld => Volatile.Read(ref held) == 1;

        public long FencingToken { get; }

        public async ValueTask DisposeAsync()
        {
            await renewalStopped.CancelAsync();
            try
            {
                await renewal;
            }
            catch (OperationCanceledException)
            {
            }

            renewalStopped.Dispose();
            Volatile.Write(ref held, 0);
            await lease.DisposeAsync();
        }

        private async Task RenewAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromMilliseconds(Math.Max(1, duration.TotalMilliseconds / 3));
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var renewed = await lease.RenewAsync(duration, cancellationToken);
                    if (!renewed)
                    {
                        Volatile.Write(ref held, 0);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}