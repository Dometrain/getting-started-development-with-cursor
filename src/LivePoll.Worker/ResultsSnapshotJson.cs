using System.Text.Json;

namespace LivePoll.Worker;

public static class ResultsSnapshotJson
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(LivePollPicture picture, bool ended, int votesPerSecond, long fencingToken)
    {
        var payload = new Payload
        {
            PollId = picture.PollId,
            Ended = ended,
            VotesPerSecond = votesPerSecond,
            FencingToken = fencingToken,
            Options = picture.Options.Select(option => new OptionPayload
            {
                OptionId = option.OptionId,
                Text = option.Text,
                Votes = option.Votes,
            }).ToArray(),
        };

        return JsonSerializer.Serialize(payload, jsonOptions);
    }

    private sealed class Payload
    {
        public required Guid PollId { get; init; }

        public required bool Ended { get; init; }

        public required int VotesPerSecond { get; init; }

        public required long FencingToken { get; init; }

        public required IReadOnlyList<OptionPayload> Options { get; init; }
    }

    private sealed class OptionPayload
    {
        public required Guid OptionId { get; init; }

        public required string Text { get; init; }

        public required long Votes { get; init; }
    }
}