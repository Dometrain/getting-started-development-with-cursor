using LivePoll.Infrastructure.Ruvio;

using Ruvio.Client;

namespace LivePoll.Api.LiveResults;

public sealed class RuvioResultsListener(
    IRuvioClient client,
    ResultsSnapshotHub hub,
    ILogger<RuvioResultsListener> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> subscriptionFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "ResultsSubscriptionFailed"),
            "Results subscription failed ({Failure}).");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await client.SubscribeAsync(RuvioKeys.ResultsChannel, OnMessageAsync, stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                subscriptionFailed(logger, exception.GetType().Name, exception);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private Task OnMessageAsync(PubSubMessage message)
    {
        if (PublishedSnapshotParser.TryParse(message.Payload, out var snapshot))
        {
            hub.Accept(snapshot);
        }

        return Task.CompletedTask;
    }
}