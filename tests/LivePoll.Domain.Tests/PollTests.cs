using LivePoll.Domain;

namespace LivePoll.Domain.Tests;

public sealed class PollTests
{
    [Fact]
    public void Create_BlankTitle_IsRejected()
    {
        Assert.Throws<DomainException>(() => Poll.Create("", ["Alpha", "Beta"]));
    }

    [Fact]
    public void Create_WhitespaceTitle_IsRejected()
    {
        Assert.Throws<DomainException>(() => Poll.Create("   ", ["Alpha", "Beta"]));
    }

    [Fact]
    public void Create_TitleLongerThan120Characters_IsRejected()
    {
        var title = new string('a', 121);

        Assert.Throws<DomainException>(() => Poll.Create(title, ["Alpha", "Beta"]));
    }

    [Fact]
    public void Create_BlankOption_IsRejected()
    {
        Assert.Throws<DomainException>(() => Poll.Create("Lunch?", ["", "Beta"]));
    }

    [Fact]
    public void Create_WhitespaceOption_IsRejected()
    {
        Assert.Throws<DomainException>(() => Poll.Create("Lunch?", ["   ", "Beta"]));
    }

    [Fact]
    public void Create_OptionShorterThanThreeCharacters_IsRejected()
    {
        Assert.Throws<DomainException>(() => Poll.Create("Lunch?", ["Ab", "Beta"]));
    }

    [Fact]
    public void Create_FewerThanTwoOptions_IsRejected()
    {
        Assert.Throws<DomainException>(() => Poll.Create("Lunch?", ["Alpha"]));
    }

    [Fact]
    public void Create_MoreThanSixOptions_IsRejected()
    {
        Assert.Throws<DomainException>(() =>
            Poll.Create("Lunch?", ["One", "Two", "Three", "Four", "Five", "Six", "Seven"]));
    }

    [Fact]
    public void OpenDurationSeconds_Is600()
    {
        Assert.Equal(600, Poll.OpenDurationSeconds);
    }

    [Fact]
    public void UpdateTitle_AfterVote_IsRejected()
    {
        var poll = Poll.Create("Lunch?", ["Alpha", "Beta"]);
        poll.CastVote("alice", poll.Options[0].Id);

        Assert.Throws<DomainException>(() => poll.UpdateTitle("Dinner?", ActorRole.Admin));
    }

    [Fact]
    public void AddOption_AfterVote_IsRejected()
    {
        var poll = Poll.Create("Lunch?", ["Alpha", "Beta"]);
        poll.CastVote("alice", poll.Options[0].Id);

        Assert.Throws<DomainException>(() => poll.AddOption("Gamma", ActorRole.Admin));
    }

    [Fact]
    public void EditOption_AfterVote_IsRejected()
    {
        var poll = Poll.Create("Lunch?", ["Alpha", "Beta"]);
        var optionId = poll.Options[0].Id;
        poll.CastVote("alice", optionId);

        Assert.Throws<DomainException>(() => poll.EditOption(optionId, "Delta", ActorRole.Admin));
    }

    [Fact]
    public void RemoveOption_AfterVote_IsRejected()
    {
        var poll = Poll.Create("Lunch?", ["Alpha", "Beta", "Gamma"]);
        poll.CastVote("alice", poll.Options[0].Id);

        Assert.Throws<DomainException>(() => poll.RemoveOption(poll.Options[1].Id, ActorRole.Admin));
    }

    [Fact]
    public void AddOption_WhenCallerIsVoter_IsRejected()
    {
        var poll = Poll.Create("Lunch?", ["Alpha", "Beta"]);

        Assert.Throws<DomainException>(() => poll.AddOption("Gamma", ActorRole.Voter));
    }

    [Fact]
    public void CastVote_SecondTimeBySameUsername_IsRejected()
    {
        var poll = Poll.Create("Lunch?", ["Alpha", "Beta"]);
        poll.CastVote("alice", poll.Options[0].Id);

        Assert.Throws<DomainException>(() => poll.CastVote("alice", poll.Options[1].Id));
    }

    [Fact]
    public void Restore_KeepsIdOptionsAndVoters()
    {
        var pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
        var optionId = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
        var poll = Poll.Restore(
            pollId,
            "Lunch spot",
            [(optionId, "Cafeteria"), (Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e"), "Food truck")],
            ["ada"]);

        Assert.Equal(pollId, poll.Id);
        Assert.Equal(optionId, poll.Options[0].Id);
        Assert.Equal("Cafeteria", poll.Options[0].Text);
        Assert.Throws<DomainException>(() => poll.UpdateTitle("Dinner spot", ActorRole.Admin));
    }
}