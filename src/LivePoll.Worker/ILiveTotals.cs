namespace LivePoll.Worker;

public interface ILiveTotals
{
    Task<IReadOnlyList<LivePollPicture>> ReadAsync(CancellationToken cancellationToken);
}