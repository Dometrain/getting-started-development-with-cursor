namespace LivePoll.Worker.Voting;

public interface IBacklogCounter
{
    Task DecrementOnceAsync(CancellationToken cancellationToken);
}