namespace LivePoll.Simulator.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow) => this.utcNow = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return utcNow;
        }
    }

    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);

        DateTimeOffset target;
        lock (gate)
        {
            target = utcNow + delta;
        }

        while (true)
        {
            ManualTimer? next = null;
            lock (gate)
            {
                foreach (var candidate in timers)
                {
                    if (!candidate.Armed || candidate.Due > target)
                    {
                        continue;
                    }

                    if (next is null || candidate.Due < next.Due)
                    {
                        next = candidate;
                    }
                }

                if (next is null)
                {
                    utcNow = target;
                    return;
                }

                utcNow = next.Due;
                next.Armed = false;
                timers.Remove(next);
            }

            next.Invoke();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider owner;
        private readonly TimerCallback callback;
        private readonly object? state;
        private bool disposed;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            this.owner = owner;
            this.callback = callback;
            this.state = state;
        }

        public DateTimeOffset Due { get; private set; }

        public bool Armed { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _ = period;

            lock (owner.gate)
            {
                if (disposed)
                {
                    return false;
                }

                if (dueTime == Timeout.InfiniteTimeSpan)
                {
                    Armed = false;
                    owner.timers.Remove(this);
                    return true;
                }

                ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);

                Due = owner.utcNow + dueTime;
                Armed = true;
                if (!owner.timers.Contains(this))
                {
                    owner.timers.Add(this);
                }
            }

            return true;
        }

        public void Invoke() => callback(state);

        public void Dispose()
        {
            lock (owner.gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Armed = false;
                owner.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}