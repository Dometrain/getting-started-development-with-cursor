using System.Diagnostics.CodeAnalysis;

using LivePoll.Application;

using Ruvio.Client;

namespace LivePoll.Infrastructure.Ruvio;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Queue is the product term for the accepted-vote work stream.")]
public sealed class RuvioVoteQueue(IRuvioClient client) : IVoteQueue
{
    public async Task EnqueueAsync(Guid pollId, string username, Guid optionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await client.AddStreamEntryAsync(
            RuvioKeys.VoteStream,
            StreamIds.New,
            (RuvioKeys.PollIdField, pollId.ToString("D")),
            (RuvioKeys.UsernameField, username),
            (RuvioKeys.OptionIdField, optionId.ToString("D")));
    }
}
