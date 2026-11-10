using System.Reflection;

using LivePoll.Api.LiveResults;
using LivePoll.Application;

using Microsoft.Extensions.Logging;

using Ruvio.Client;

namespace LivePoll.Api.Tests;

public sealed class ResultsSubscriptionLogTests
{
    [Fact]
    public async Task SubscribeFailure_IsLogged()
    {
        var client = DispatchProxy.Create<IRuvioClient, ThrowingSubscriber>();
        var failure = new InvalidOperationException("subscription failed");
        ((ThrowingSubscriber)(object)client).Failure = failure;
        var logger = new SubscriptionLogger();
        var listener = new RuvioResultsListener(client, new ResultsSnapshotHub(new SystemClock()), logger);

        await listener.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (logger.Exceptions.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        await listener.StopAsync(CancellationToken.None);

        Assert.Contains(
            logger.Exceptions,
            exception => exception.ToString().Contains("subscription failed", StringComparison.Ordinal));
    }

    private sealed class SubscriptionLogger : ILogger<RuvioResultsListener>
    {
        public List<Exception> Exceptions { get; } = [];

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
            if (exception is not null)
            {
                Exceptions.Add(exception);
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static readonly EmptyScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    public class ThrowingSubscriber : DispatchProxy
    {
        public Exception Failure { get; set; } = new InvalidOperationException("subscription failed");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (string.Equals(targetMethod?.Name, "SubscribeAsync", StringComparison.Ordinal))
            {
                throw Failure;
            }

            if (targetMethod?.ReturnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            return null;
        }
    }
}