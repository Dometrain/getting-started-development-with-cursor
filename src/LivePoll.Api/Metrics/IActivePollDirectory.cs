namespace LivePoll.Api.Metrics;

public interface IActivePollDirectory
{
    Task<IReadOnlyList<ActivePollEntry>> ListAsync(CancellationToken cancellationToken);
}

public sealed record ActivePollEntry(Guid PollId, DateTimeOffset EndsAt);
