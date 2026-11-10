namespace LivePoll.Infrastructure.Ruvio;

public static class RuvioKeys
{
    public const string VoteStream = "votes";

    public const string ConsumerGroup = "workers";

    public const string TotalVotes = "metrics:votes:total";

    public const string Backlog = "metrics:votes:backlog";

    public const string ActivePolls = "polls:active";

    public const string ServerClock = "metrics:clock";

    public const string ResultsChannel = "results";

    public const string PollIdField = "pollId";

    public const string UsernameField = "username";

    public const string OptionIdField = "optionId";

    public const long OneVoteTtlMilliseconds = 86_400_000;

    public static string Once(Guid pollId, string username) => $"vote:once:{pollId:D}:{username}";

    public static string EndsAt(Guid pollId) => $"poll:{pollId:D}:ends-at";

    public static string OptionVotes(Guid pollId, Guid optionId) => $"metrics:votes:{pollId:D}:{optionId:D}";
}
