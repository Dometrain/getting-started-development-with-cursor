using LivePoll.Application;

namespace LivePoll.Application.Tests;

public sealed class VotesPerSecond_FromStreamEntryIds
{
    [Fact]
    public void FromStreamEntryIds_CountsPreviousRuvioServerSecondOnly()
    {
        // Ruvio server time is 17:00:05.250 → previous second is [17:00:04.000, 17:00:05.000).
        var ruvioServerUnixMilliseconds = new DateTimeOffset(2026, 11, 7, 17, 0, 5, 250, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        var previousSecondStart = new DateTimeOffset(2026, 11, 7, 17, 0, 4, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        var currentSecondStart = new DateTimeOffset(2026, 11, 7, 17, 0, 5, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        var entryIds = new[]
        {
            $"{previousSecondStart - 1}-0",
            $"{previousSecondStart}-0",
            $"{previousSecondStart + 500}-1",
            $"{currentSecondStart - 1}-2",
            $"{currentSecondStart}-0",
            $"{ruvioServerUnixMilliseconds}-3",
        };

        var votesPerSecond = VotesPerSecond.FromStreamEntryIds(entryIds, ruvioServerUnixMilliseconds);

        Assert.Equal(3, votesPerSecond);
    }

    [Fact]
    public void FromStreamEntryIds_DoesNotUseApiClock()
    {
        var ruvioServerUnixMilliseconds = new DateTimeOffset(2026, 11, 7, 17, 0, 10, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        var previousSecondStart = ruvioServerUnixMilliseconds - 1000;
        var apiSkewedClockUnixMilliseconds = ruvioServerUnixMilliseconds + 30_000;
        var entryIds = new[]
        {
            $"{previousSecondStart}-0",
            $"{apiSkewedClockUnixMilliseconds - 500}-0",
        };

        var votesPerSecond = VotesPerSecond.FromStreamEntryIds(entryIds, ruvioServerUnixMilliseconds);

        Assert.Equal(1, votesPerSecond);
    }
}
