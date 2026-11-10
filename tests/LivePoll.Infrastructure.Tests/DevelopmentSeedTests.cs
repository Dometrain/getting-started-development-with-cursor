using System.Text.RegularExpressions;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using LivePoll.Application;
using LivePoll.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace LivePoll.Infrastructure.Tests;

public sealed class DevelopmentSeedTests : IAsyncLifetime
{
    private readonly SeedPostgres postgres = new();

    public Task InitializeAsync() => postgres.StartAsync();

    public Task DisposeAsync() => postgres.StopAsync();

    [Fact]
    public async Task DevelopmentSeed_WritesEndTimeAndActivePoll()
    {
        await using var db = postgres.CreateContext();
        await db.Database.MigrateAsync();
        var endTimes = new RecordingEndTimes();
        var activePolls = new RecordingActivePolls();

        await DevelopmentSeed.ApplyAsync(db, endTimes, activePolls);

        var polls = await db.Polls.AsNoTracking().ToListAsync();
        Assert.NotEmpty(polls);
        Assert.Equal(polls.Count, endTimes.Saved.Count);
        Assert.Equal(polls.Count, activePolls.Added.Count);
        foreach (var poll in polls)
        {
            Assert.Contains(
                endTimes.Saved,
                saved => saved.PollId == poll.Id && saved.EndsAt.ToUnixTimeMilliseconds() == poll.EndsAt.ToUnixTimeMilliseconds());
            Assert.Contains(
                activePolls.Added,
                added => added.PollId == poll.Id && added.EndsAt.ToUnixTimeMilliseconds() == poll.EndsAt.ToUnixTimeMilliseconds());
        }
    }

    private sealed class RecordingEndTimes : IPollEndTimeWriter
    {
        public List<(Guid PollId, DateTimeOffset EndsAt)> Saved { get; } = [];

        public Task SaveEndsAtAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
        {
            Saved.Add((pollId, endsAt));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingActivePolls : IActivePollWriter
    {
        public List<(Guid PollId, DateTimeOffset EndsAt)> Added { get; } = [];

        public Task AddAsync(Guid pollId, DateTimeOffset endsAt, CancellationToken cancellationToken)
        {
            Added.Add((pollId, endsAt));
            return Task.CompletedTask;
        }
    }

    private sealed class SeedPostgres
    {
        private IContainer? container;

        public string ConnectionString { get; private set; } = "";

        public async Task StartAsync()
        {
            var compose = File.ReadAllText(Path.Combine(RepoRoot.Find(), "docker-compose.yml"));
            var image = Regex.Match(compose, @"image:\s*(postgres:\S+)").Groups[1].Value;
            container = new ContainerBuilder(image)
                .WithEnvironment("POSTGRES_USER", "livepoll")
                .WithEnvironment("POSTGRES_PASSWORD", "livepoll")
                .WithEnvironment("POSTGRES_DB", "livepoll")
                .WithPortBinding(5432, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(
                    5432,
                    wait =>
                    {
                        wait.WithTimeout(TimeSpan.FromMinutes(3));
                    }))
                .Build();
            await container.StartAsync();
            var port = container.GetMappedPublicPort(5432);
            ConnectionString =
                $"Host=127.0.0.1;Port={port};Database=livepoll;Username=livepoll;Password=livepoll";
            await WaitUntilPostgresAcceptsAsync();
        }

        private async Task WaitUntilPostgresAcceptsAsync()
        {
            var deadline = DateTime.UtcNow.AddMinutes(1);
            Exception? last = null;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await using var db = CreateContext();
                    await db.Database.OpenConnectionAsync();
                    return;
                }
                catch (Exception exception)
                {
                    last = exception;
                    await Task.Delay(500);
                }
            }

            throw new InvalidOperationException("Postgres did not accept a connection.", last);
        }

        public LivePollDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<LivePollDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            return new LivePollDbContext(options);
        }

        public async Task StopAsync()
        {
            if (container is not null)
            {
                await container.DisposeAsync();
            }
        }
    }
}