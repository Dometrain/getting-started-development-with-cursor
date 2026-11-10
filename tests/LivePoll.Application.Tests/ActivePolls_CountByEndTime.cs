using LivePoll.Application;

namespace LivePoll.Application.Tests;

public sealed class ActivePolls_CountByEndTime
{
    [Fact]
    public void CountByEndTime_IncludesOnlyEntriesAfterNow()
    {
        var now = new DateTimeOffset(2026, 11, 7, 17, 5, 0, TimeSpan.Zero);
        var endTimes = new[]
        {
            now.AddMinutes(-1),
            now,
            now.AddSeconds(1),
            now.AddMinutes(10),
        };

        var active = ActivePolls.CountByEndTime(endTimes, now);

        Assert.Equal(2, active);
    }

    [Fact]
    public void CountByEndTime_WhenEmpty_IsZero()
    {
        var now = new DateTimeOffset(2026, 11, 7, 17, 5, 0, TimeSpan.Zero);

        var active = ActivePolls.CountByEndTime([], now);

        Assert.Equal(0, active);
    }
}
