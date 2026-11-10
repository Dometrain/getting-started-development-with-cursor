using LivePoll.Application;

namespace LivePoll.Integration.Voting;

internal sealed class MutableClock(DateTimeOffset utcNow) : IClock
{
    public static MutableClock UnixNow() =>
        new(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
