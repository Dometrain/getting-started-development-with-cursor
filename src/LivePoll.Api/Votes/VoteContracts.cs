namespace LivePoll.Api.Votes;

public sealed record CastVoteRequest(Guid? OptionId);

public sealed record VoteAcceptedResponse(Guid PollId, Guid OptionId, string Username);
