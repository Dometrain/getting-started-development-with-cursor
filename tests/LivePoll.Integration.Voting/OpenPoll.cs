namespace LivePoll.Integration.Voting;

public sealed record OpenOption(Guid Id, string Text);

public sealed record OpenPoll(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset EndsAt,
    IReadOnlyList<OpenOption> Options);
