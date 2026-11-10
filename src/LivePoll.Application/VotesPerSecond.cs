namespace LivePoll.Application;

public static class VotesPerSecond
{
    public static int FromStreamEntryIds(IEnumerable<string> streamEntryIds, long ruvioServerUnixMilliseconds)
    {
        var currentSecondStart = ruvioServerUnixMilliseconds - (ruvioServerUnixMilliseconds % 1000);
        var previousSecondStart = currentSecondStart - 1000;
        var count = 0;

        foreach (var entryId in streamEntryIds)
        {
            if (!TryParseEntryTimestamp(entryId, out var entryMilliseconds))
            {
                continue;
            }

            if (entryMilliseconds >= previousSecondStart && entryMilliseconds < currentSecondStart)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryParseEntryTimestamp(string entryId, out long milliseconds)
    {
        milliseconds = 0;
        var separator = entryId.IndexOf('-');
        if (separator <= 0)
        {
            return false;
        }

        return long.TryParse(entryId.AsSpan(0, separator), out milliseconds);
    }
}