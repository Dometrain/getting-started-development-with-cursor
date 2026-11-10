namespace LivePoll.Application;

public static class ActivePolls
{
    public static int CountByEndTime(IEnumerable<DateTimeOffset> endTimes, DateTimeOffset now) =>
        endTimes.Count(endsAt => endsAt > now);
}