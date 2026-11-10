namespace LivePoll.Api.Polls;

public sealed record CreatePollRequest(string? Title, IReadOnlyList<string>? Options);

public sealed record UpdatePollTitleRequest(string? Title);

public sealed record PollOptionTextRequest(string? Text);

public sealed record PollOptionResponse(Guid Id, string Text);

public sealed record PollResponse(
    Guid Id,
    string Title,
    IReadOnlyList<PollOptionResponse> Options,
    int DurationSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset EndsAt,
    bool Ended);