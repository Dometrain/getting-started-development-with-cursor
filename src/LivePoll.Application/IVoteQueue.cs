using System.Diagnostics.CodeAnalysis;

namespace LivePoll.Application;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Queue is the product term for the accepted-vote work stream.")]
public interface IVoteQueue
{
    Task EnqueueAsync(Guid pollId, string username, Guid optionId, CancellationToken cancellationToken);
}