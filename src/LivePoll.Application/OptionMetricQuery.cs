namespace LivePoll.Application;

public sealed record OptionMetricQuery(Guid PollId, Guid OptionId, string Text);