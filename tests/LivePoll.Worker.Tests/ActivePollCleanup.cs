using LivePoll.Worker;

using Microsoft.Extensions.Hosting;

namespace LivePoll.Worker.Tests;

public sealed class ActivePollCleanup
{
    [Fact]
    public async Task RemovesEntriesAtOrBeforeNow_WithoutACloseJob()
    {
        var world = new PublishWorld();
        var endedId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
        var openId = Guid.Parse("9c0d1e2f-3a4b-4c5d-8e6f-708192a3b4c5");
        var now = world.Clock.UtcNow;
        world.ActivePolls.Polls.Add(new ActivePollEntry(endedId, now));
        world.ActivePolls.Polls.Add(new ActivePollEntry(openId, now.AddMilliseconds(1)));

        await world.CreatePublisher().PublishOnceAsync();

        Assert.Equal([now], world.ActivePolls.RemovedAt);
        Assert.DoesNotContain(world.ActivePolls.Polls, poll => poll.PollId == endedId);
        Assert.Contains(world.ActivePolls.Polls, poll => poll.PollId == openId);

        var hosted = typeof(SnapshotPublishService).Assembly.GetTypes()
            .Where(type => type.BaseType == typeof(BackgroundService))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["SnapshotPublishService", "Worker"], hosted);
        Assert.DoesNotContain(hosted, name => name.Contains("Close", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hosted, name => name.Contains("Schedule", StringComparison.OrdinalIgnoreCase));

        var cleaner = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivePoll.Worker", "RuvioActivePollCleaner.cs"));
        Assert.Contains("RemoveSortedSetRangeByScoreAsync", cleaner, StringComparison.Ordinal);
        Assert.Contains("\"-inf\"", cleaner, StringComparison.Ordinal);
        Assert.Contains("ToUnixTimeMilliseconds", cleaner, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}