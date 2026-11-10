namespace LivePoll.Integration.Voting;

[CollectionDefinition(Name)]
public sealed class VoteFlowGroup : ICollectionFixture<VoteFlowFixture>
{
    public const string Name = "VoteFlow";
}
