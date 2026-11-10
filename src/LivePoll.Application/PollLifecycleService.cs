using LivePoll.Domain;

namespace LivePoll.Application;

public sealed class PollLifecycleService
{
    private readonly IPollRepository polls;
    private readonly IClock clock;
    private readonly IPollEndTimeWriter endTimes;
    private readonly IActivePollWriter activePolls;

    public PollLifecycleService(
        IPollRepository polls,
        IClock clock,
        IPollEndTimeWriter endTimes,
        IActivePollWriter activePolls)
    {
        this.polls = polls;
        this.clock = clock;
        this.endTimes = endTimes;
        this.activePolls = activePolls;
    }

    public async Task<PollView> CreateAsync(
        string username,
        string title,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken = default)
    {
        EnsureSignedIn(username);

        var createdAt = clock.UtcNow;
        var endsAt = createdAt.AddSeconds(Poll.OpenDurationSeconds);
        var aggregate = Poll.Create(title, options);
        var stored = new StoredPoll
        {
            Aggregate = aggregate,
            CreatedAt = createdAt,
            EndsAt = endsAt,
        };

        await polls.SaveAsync(stored, cancellationToken);
        await endTimes.SaveEndsAtAsync(aggregate.Id, endsAt, cancellationToken);
        await activePolls.AddAsync(aggregate.Id, endsAt, cancellationToken);
        return ToView(stored);
    }

    public async Task<PollView> UpdateTitleAsync(
        Guid pollId,
        string title,
        ActorRole role,
        CancellationToken cancellationToken = default)
    {
        var stored = await RequirePollAsync(pollId, cancellationToken);
        stored.Aggregate.UpdateTitle(title, role);
        await polls.SaveAsync(stored, cancellationToken);
        return ToView(stored);
    }

    public async Task<PollView> AddOptionAsync(
        Guid pollId,
        string text,
        ActorRole role,
        CancellationToken cancellationToken = default)
    {
        var stored = await RequirePollAsync(pollId, cancellationToken);
        stored.Aggregate.AddOption(text, role);
        await polls.SaveAsync(stored, cancellationToken);
        return ToView(stored);
    }

    public async Task<PollView> EditOptionAsync(
        Guid pollId,
        Guid optionId,
        string text,
        ActorRole role,
        CancellationToken cancellationToken = default)
    {
        var stored = await RequirePollAsync(pollId, cancellationToken);
        stored.Aggregate.EditOption(optionId, text, role);
        await polls.SaveAsync(stored, cancellationToken);
        return ToView(stored);
    }

    public async Task<PollView> RemoveOptionAsync(
        Guid pollId,
        Guid optionId,
        ActorRole role,
        CancellationToken cancellationToken = default)
    {
        var stored = await RequirePollAsync(pollId, cancellationToken);
        stored.Aggregate.RemoveOption(optionId, role);
        await polls.SaveAsync(stored, cancellationToken);
        return ToView(stored);
    }

    private async Task<StoredPoll> RequirePollAsync(Guid pollId, CancellationToken cancellationToken)
    {
        var stored = await polls.GetAsync(pollId, cancellationToken);
        if (stored is null)
        {
            throw new ApplicationException("Poll was not found.");
        }

        return stored;
    }

    private static void EnsureSignedIn(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ApplicationException("A signed-in user is required.");
        }
    }

    private PollView ToView(StoredPoll stored)
    {
        var now = clock.UtcNow;
        return new PollView
        {
            Id = stored.Aggregate.Id,
            Title = stored.Aggregate.Title,
            Options = stored.Aggregate.Options
                .Select(option => new PollOptionView { Id = option.Id, Text = option.Text })
                .ToList(),
            DurationSeconds = Poll.OpenDurationSeconds,
            CreatedAt = stored.CreatedAt,
            EndsAt = stored.EndsAt,
            Ended = now >= stored.EndsAt,
        };
    }
}