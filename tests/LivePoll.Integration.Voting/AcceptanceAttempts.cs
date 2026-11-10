using LivePoll.Application;

using PollException = LivePoll.Application.ApplicationException;

namespace LivePoll.Integration.Voting;

internal static class AcceptanceAttempts
{
    public static async Task<(bool Accepted, PollException? Error)> RecordAsync(Task<VoteAcceptanceResult> attempt)
    {
        try
        {
            await attempt;
            return (true, null);
        }
        catch (PollException error)
        {
            return (false, error);
        }
    }
}
