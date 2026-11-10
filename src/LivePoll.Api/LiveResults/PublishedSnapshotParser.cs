using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace LivePoll.Api.LiveResults;

public static class PublishedSnapshotParser
{
    public static bool TryParse(string? payload, [NotNullWhen(true)] out ParsedResultsSnapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        WireSnapshot? wire;
        try
        {
            wire = JsonSerializer.Deserialize<WireSnapshot>(payload, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return false;
        }

        if (wire is null || wire.PollId == Guid.Empty || wire.Options is null || wire.Options.Count < 2)
        {
            return false;
        }

        var options = new List<OptionTotalResponse>(wire.Options.Count);
        foreach (var option in wire.Options)
        {
            if (option.OptionId == Guid.Empty || string.IsNullOrWhiteSpace(option.Text))
            {
                return false;
            }

            options.Add(new OptionTotalResponse(option.OptionId, option.Text, option.Votes));
        }

        snapshot = new ParsedResultsSnapshot(
            wire.PollId,
            wire.Ended,
            wire.VotesPerSecond,
            wire.FencingToken,
            options);
        return true;
    }

    private sealed class WireSnapshot
    {
        public Guid PollId { get; set; }

        public bool Ended { get; set; }

        public int VotesPerSecond { get; set; }

        public long FencingToken { get; set; }

        public List<WireOption>? Options { get; set; }
    }

    private sealed class WireOption
    {
        public Guid OptionId { get; set; }

        public string? Text { get; set; }

        public long Votes { get; set; }
    }
}
