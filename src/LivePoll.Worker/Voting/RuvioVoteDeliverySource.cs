using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Worker.Voting;

public sealed class RuvioVoteDeliverySource : IVoteDeliverySource
{
    public const long ClaimMinIdleMilliseconds = 1000;

    private readonly IRuvioClient commands;
    private readonly IRuvioClient blockingReads;
    private readonly string consumerName;
    private bool groupReady;

    public RuvioVoteDeliverySource(IRuvioClient commands, IRuvioClient blockingReads)
        : this(commands, blockingReads, BuildConsumerName())
    {
    }

    public RuvioVoteDeliverySource(
        IRuvioClient commands,
        IRuvioClient blockingReads,
        string consumerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        if (ReferenceEquals(commands, blockingReads))
        {
            throw new ArgumentException("The blocking read must use its own Ruvio client.", nameof(blockingReads));
        }

        this.commands = commands;
        this.blockingReads = blockingReads;
        this.consumerName = consumerName;
    }

    public IRuvioClient Commands => commands;

    public IRuvioClient BlockingReads => blockingReads;

    public async Task<IReadOnlyList<VoteDelivery>> ReadAsync(CancellationToken cancellationToken)
    {
        await EnsureGroupAsync(cancellationToken);

        var deliveries = new List<VoteDelivery>();
        await ClaimIdleAsync(deliveries, cancellationToken);

        var read = await blockingReads.GetStreamGroupUpdatesAsync(
            RuvioKeys.ConsumerGroup,
            consumerName,
            RuvioKeys.VoteStream,
            StreamIds.NewMessages,
            new StreamReadOptions
            {
                Count = 32,
                Block = TimeSpan.FromSeconds(2),
            },
            cancellationToken);

        foreach (var stream in read)
        {
            foreach (var entry in stream.Entries)
            {
                deliveries.Add(await ToDeliveryAsync(entry, cancellationToken));
            }
        }

        return deliveries;
    }

    public async Task AcknowledgeAsync(string entryId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await commands.AcknowledgeStreamAsync(
            RuvioKeys.VoteStream,
            RuvioKeys.ConsumerGroup,
            [entryId]);
    }

    public async Task KeepAsideAsync(VoteDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await commands.AddStreamEntryAsync(
            WorkerStreamKeys.SideStream,
            StreamIds.New,
            (RuvioKeys.PollIdField, delivery.PollId.ToString("D")),
            (RuvioKeys.UsernameField, delivery.Username),
            (RuvioKeys.OptionIdField, delivery.OptionId.ToString("D")),
            ("entryId", delivery.EntryId),
            ("deliveryCount", delivery.DeliveryCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private async Task ClaimIdleAsync(List<VoteDelivery> deliveries, CancellationToken cancellationToken)
    {
        var pending = await commands.GetStreamPendingAsync(
            RuvioKeys.VoteStream,
            RuvioKeys.ConsumerGroup,
            StreamIds.Min,
            StreamIds.Max,
            32,
            consumer: null,
            minIdle: TimeSpan.FromMilliseconds(ClaimMinIdleMilliseconds),
            cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var ids = pending.Select(entry => entry.Id).ToArray();
        var claimed = await commands.ClaimStreamEntriesAsync(
            RuvioKeys.VoteStream,
            RuvioKeys.ConsumerGroup,
            consumerName,
            ClaimMinIdleMilliseconds,
            ids);

        var counts = pending.ToDictionary(entry => entry.Id, entry => entry.DeliveryCount, StringComparer.Ordinal);
        foreach (var entry in claimed)
        {
            var deliveryCount = counts.GetValueOrDefault(entry.Id, 1);
            deliveries.Add(Parse(entry, deliveryCount));
        }
    }

    private async Task<VoteDelivery> ToDeliveryAsync(StreamEntry entry, CancellationToken cancellationToken)
    {
        var pending = await commands.GetStreamPendingAsync(
            RuvioKeys.VoteStream,
            RuvioKeys.ConsumerGroup,
            entry.Id,
            entry.Id,
            1,
            consumer: null,
            minIdle: null,
            cancellationToken);
        var deliveryCount = pending.Count > 0 ? pending[0].DeliveryCount : 1L;
        return Parse(entry, deliveryCount);
    }

    private static VoteDelivery Parse(StreamEntry entry, long deliveryCount)
    {
        var pollId = Guid.Parse(Field(entry, RuvioKeys.PollIdField));
        var username = Field(entry, RuvioKeys.UsernameField);
        var optionId = Guid.Parse(Field(entry, RuvioKeys.OptionIdField));
        return new VoteDelivery(entry.Id, pollId, username, optionId, deliveryCount);
    }

    private static string Field(StreamEntry entry, string name) =>
        entry.Fields.Single(pair => pair.Key == name).Value;

    private async Task EnsureGroupAsync(CancellationToken cancellationToken)
    {
        if (groupReady)
        {
            return;
        }

        try
        {
            await commands.CreateStreamGroupAsync(
                RuvioKeys.VoteStream,
                RuvioKeys.ConsumerGroup,
                "0",
                createStream: true,
                cancellationToken);
        }
        catch (RuvioException)
        {
            // Group already exists.
        }

        groupReady = true;
    }

    private static string BuildConsumerName() =>
        $"{Environment.MachineName}-{Guid.NewGuid():N}";
}