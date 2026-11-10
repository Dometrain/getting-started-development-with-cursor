using LivePoll.Application;
using LivePoll.Domain;

namespace LivePoll.Application.Tests;

public sealed class PollLifecycleTests
{
    private readonly FixedClock clock = new(new DateTimeOffset(2026, 11, 7, 17, 0, 0, TimeSpan.Zero));
    private readonly InMemoryPollRepository polls = new();
    private readonly RecordingEndTimes endTimes = new();
    private readonly RecordingActivePolls activePolls = new();

    private PollLifecycleService CreateService() => new(polls, clock, endTimes, activePolls);

    [Fact]
    public async Task Create_SignedInUser_SetsEndsAtTenMinutesAfterCreatedAt()
    {
        var service = CreateService();

        var poll = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        Assert.Equal(clock.UtcNow, poll.CreatedAt);
        Assert.Equal(clock.UtcNow.AddMinutes(10), poll.EndsAt);
        Assert.Equal(600, poll.DurationSeconds);
        Assert.Equal("Lunch spot", poll.Title);
        Assert.Equal(2, poll.Options.Count);
        Assert.False(poll.Ended);
    }

    [Fact]
    public async Task Create_SignedInUser_RecordsEndTimeAndActivePoll()
    {
        var service = CreateService();

        var poll = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        var saved = Assert.Single(endTimes.Saved);
        Assert.Equal(poll.Id, saved.PollId);
        Assert.Equal(poll.EndsAt, saved.EndsAt);
        var added = Assert.Single(activePolls.Added);
        Assert.Equal(poll.Id, added.PollId);
        Assert.Equal(poll.EndsAt, added.EndsAt);
    }

    [Fact]
    public async Task Create_BlankUsername_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<LivePoll.Application.ApplicationException>(() =>
            service.CreateAsync(" ", "Lunch spot", ["Cafeteria", "Food truck"]));
    }

    [Fact]
    public async Task Create_BlankTitle_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync("ada", "", ["Cafeteria", "Food truck"]));
    }

    [Fact]
    public async Task Create_TitleLongerThan120Characters_IsRejected()
    {
        var service = CreateService();
        var title = new string('a', 121);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync("ada", title, ["Cafeteria", "Food truck"]));
    }

    [Fact]
    public async Task Create_BlankOption_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync("ada", "Lunch spot", ["", "Food truck"]));
    }

    [Fact]
    public async Task Create_OptionShorterThanThreeCharacters_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync("ada", "Lunch spot", ["Ab", "Food truck"]));
    }

    [Fact]
    public async Task Create_FewerThanTwoOptions_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync("ada", "Lunch spot", ["Cafeteria"]));
    }

    [Fact]
    public async Task Create_MoreThanSixOptions_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(
                "ada",
                "Lunch spot",
                ["One", "Two", "Three", "Four", "Five", "Six", "Seven"]));
    }

    [Fact]
    public async Task UpdateTitle_WhenAdminBeforeVotes_Succeeds()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        var updated = await service.UpdateTitleAsync(created.Id, "Dinner spot", ActorRole.Admin);

        Assert.Equal("Dinner spot", updated.Title);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(created.EndsAt, updated.EndsAt);
    }

    [Fact]
    public async Task UpdateTitle_WhenVoter_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateTitleAsync(created.Id, "Dinner spot", ActorRole.Voter));
    }

    [Fact]
    public async Task AddOption_WhenAdminBeforeVotes_Succeeds()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        var updated = await service.AddOptionAsync(created.Id, "Bistro", ActorRole.Admin);

        Assert.Equal(3, updated.Options.Count);
        Assert.Contains(updated.Options, o => o.Text == "Bistro");
    }

    [Fact]
    public async Task EditOption_WhenAdminBeforeVotes_Succeeds()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);
        var optionId = created.Options[0].Id;

        var updated = await service.EditOptionAsync(created.Id, optionId, "Cafe Nora", ActorRole.Admin);

        Assert.Equal("Cafe Nora", updated.Options.Single(o => o.Id == optionId).Text);
    }

    [Fact]
    public async Task RemoveOption_WhenAdminBeforeVotes_Succeeds()
    {
        var service = CreateService();
        var created = await service.CreateAsync(
            "ada",
            "Lunch spot",
            ["Cafeteria", "Food truck", "Bistro"]);
        var optionId = created.Options[2].Id;

        var updated = await service.RemoveOptionAsync(created.Id, optionId, ActorRole.Admin);

        Assert.Equal(2, updated.Options.Count);
        Assert.DoesNotContain(updated.Options, o => o.Id == optionId);
    }

    [Fact]
    public async Task AddOption_WhenVoter_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddOptionAsync(created.Id, "Bistro", ActorRole.Voter));
    }

    [Fact]
    public async Task EditOption_WhenVoter_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.EditOptionAsync(created.Id, created.Options[0].Id, "Cafe Nora", ActorRole.Voter));
    }

    [Fact]
    public async Task RemoveOption_WhenVoter_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync(
            "ada",
            "Lunch spot",
            ["Cafeteria", "Food truck", "Bistro"]);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RemoveOptionAsync(created.Id, created.Options[2].Id, ActorRole.Voter));
    }

    [Fact]
    public async Task AddOption_WhenAlreadySix_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync(
            "ada",
            "Lunch spot",
            ["One", "Two", "Three", "Four", "Five", "Six"]);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddOptionAsync(created.Id, "Seven", ActorRole.Admin));
    }

    [Fact]
    public async Task RemoveOption_WhenOnlyTwoRemain_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RemoveOptionAsync(created.Id, created.Options[0].Id, ActorRole.Admin));
    }

    [Fact]
    public async Task UpdateTitle_AfterVote_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);
        await CastVoteAsync(created.Id, "alice");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateTitleAsync(created.Id, "Dinner spot", ActorRole.Admin));
    }

    [Fact]
    public async Task AddOption_AfterVote_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);
        await CastVoteAsync(created.Id, "alice");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.AddOptionAsync(created.Id, "Bistro", ActorRole.Admin));
    }

    [Fact]
    public async Task EditOption_AfterVote_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync("ada", "Lunch spot", ["Cafeteria", "Food truck"]);
        await CastVoteAsync(created.Id, "alice");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.EditOptionAsync(created.Id, created.Options[0].Id, "Cafe Nora", ActorRole.Admin));
    }

    [Fact]
    public async Task RemoveOption_AfterVote_IsRejected()
    {
        var service = CreateService();
        var created = await service.CreateAsync(
            "ada",
            "Lunch spot",
            ["Cafeteria", "Food truck", "Bistro"]);
        await CastVoteAsync(created.Id, "alice");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RemoveOptionAsync(created.Id, created.Options[2].Id, ActorRole.Admin));
    }

    [Fact]
    public async Task UpdateTitle_WhenPollMissing_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<LivePoll.Application.ApplicationException>(() =>
            service.UpdateTitleAsync(Guid.NewGuid(), "Dinner spot", ActorRole.Admin));
    }

    private async Task CastVoteAsync(Guid pollId, string username)
    {
        var stored = await polls.GetAsync(pollId, CancellationToken.None);
        Assert.NotNull(stored);
        stored.Aggregate.CastVote(username, stored.Aggregate.Options[0].Id);
        await polls.SaveAsync(stored, CancellationToken.None);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class RecordingEndTimes : IPollEndTimeWriter
    {
        public List<(Guid PollId, DateTimeOffset EndsAt)> Saved { get; } = [];

        public Task SaveEndsAtAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
        {
            Saved.Add((pollId, endsAt));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingActivePolls : IActivePollWriter
    {
        public List<(Guid PollId, DateTimeOffset EndsAt)> Added { get; } = [];

        public Task AddAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
        {
            Added.Add((pollId, endsAt));
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPollRepository : IPollRepository
    {
        private readonly Dictionary<Guid, StoredPoll> items = new();

        public Task SaveAsync(StoredPoll poll, CancellationToken cancellationToken)
        {
            items[poll.Aggregate.Id] = poll;
            return Task.CompletedTask;
        }

        public Task<StoredPoll?> GetAsync(Guid pollId, CancellationToken cancellationToken)
        {
            items.TryGetValue(pollId, out var poll);
            return Task.FromResult(poll);
        }
    }
}