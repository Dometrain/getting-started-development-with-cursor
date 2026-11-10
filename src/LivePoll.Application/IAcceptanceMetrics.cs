namespace LivePoll.Application;

public interface IAcceptanceMetrics
{
    Task IncrementOnAcceptanceAsync(Guid pollId, Guid optionId, CancellationToken cancellationToken);
}