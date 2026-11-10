namespace LivePoll.Worker.Voting;

public sealed class VoteOptionMissingException : Exception
{
    public VoteOptionMissingException()
        : base("The vote option is not stored.")
    {
    }
}