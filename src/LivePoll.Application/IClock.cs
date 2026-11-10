namespace LivePoll.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}