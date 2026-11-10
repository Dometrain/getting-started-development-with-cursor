namespace LivePoll.Application;

public interface IOneVoteMarker
{
    Task<bool> TryAcceptAsync(Guid pollId, string username, CancellationToken cancellationToken);
}