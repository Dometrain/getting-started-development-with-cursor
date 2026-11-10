using Microsoft.Extensions.Logging;

namespace LivePoll.Worker.Tests;

public sealed class PublisherLeaseLogs
{
    private readonly Guid pollId = Guid.Parse("3f1c0a2e-7b4d-4e1a-9c3f-6a8b2d4e5f70");
    private readonly Guid optionA = Guid.Parse("8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");
    private readonly Guid optionB = Guid.Parse("1b2c3d4e-5f6a-4b7c-8d9e-0f1a2b3c4d5e");

    [Fact]
    public async Task PublishOnce_Acquired_LogsMachineNameAndFencingToken()
    {
        var logger = new LeaseLogger();
        await PublishOnceAsync(logger, fencingToken: 41);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information
                && entry.Message.Contains("acquired", StringComparison.OrdinalIgnoreCase)
                && entry.Message.Contains(Environment.MachineName, StringComparison.Ordinal)
                && entry.Message.Contains("41", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishOnce_Released_LogsMachineNameAndFencingToken()
    {
        var logger = new LeaseLogger();
        await PublishOnceAsync(logger, fencingToken: 42);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information
                && entry.Message.Contains("lost", StringComparison.OrdinalIgnoreCase)
                && entry.Message.Contains(Environment.MachineName, StringComparison.Ordinal)
                && entry.Message.Contains("42", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishOnce_PublishThrows_LogsTheFailure()
    {
        var world = new PublishWorld();
        world.Lock.NextToken = 7;
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 4, 1)];
        var failure = new InvalidOperationException("publish failed");
        world.Results.Failure = failure;
        var logger = new LeaseLogger();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreatePublisher(world, logger).PublishOnceAsync());

        Assert.Same(failure, thrown);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, failure));
    }

    private async Task PublishOnceAsync(LeaseLogger logger, long fencingToken)
    {
        var world = new PublishWorld();
        world.Lock.NextToken = fencingToken;
        world.Totals.Pictures = [world.Picture(pollId, optionA, optionB, 4, 1)];
        await CreatePublisher(world, logger).PublishOnceAsync();
    }

    private static SnapshotPublisher CreatePublisher(PublishWorld world, LeaseLogger logger) =>
        new(world.Lock, world.Totals, world.ActivePolls, world.Results, world.Fence, world.Clock, logger);

    private sealed class LeaseLogger : ILogger<SnapshotPublisher>
    {
        public List<LeaseLog> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => EmptyScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LeaseLog(logLevel, formatter(state, exception), exception));
        }

        private sealed class EmptyScope : IDisposable
        {
            public static readonly EmptyScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record LeaseLog(LogLevel Level, string Message, Exception? Exception);
}