namespace LivePoll.Worker;

public interface IPublishLock
{
    Task<IPublishLockHold?> TryAcquireAsync(TimeSpan lease, CancellationToken cancellationToken);
}

public interface IPublishLockHold : IAsyncDisposable
{
    bool IsHeld { get; }

    long FencingToken { get; }
}