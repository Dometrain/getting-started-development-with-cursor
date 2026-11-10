namespace LivePoll.Infrastructure.Tests;

public sealed class VoteQueue_UsesNoBrokerOtherThanRuvio
{
    [Fact]
    public void Infrastructure_ReferencesRuvioAndNoOtherBroker()
    {
        var infrastructure = Path.Combine(RepoRoot.Find(), "src", "LivePoll.Infrastructure");
        var text = string.Join(
            '\n',
            Directory.EnumerateFiles(infrastructure, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.Contains("Ruvio.Client", text, StringComparison.Ordinal);
        foreach (var broker in new[]
        {
            "RabbitMQ",
            "MassTransit",
            "Confluent.Kafka",
            "NATS.Client",
            "ActiveMQ",
            "EasyNetQ",
            "Rebus",
            "Azure.Messaging.ServiceBus",
            "Amazon.SQS",
        })
        {
            Assert.DoesNotContain(broker, text, StringComparison.OrdinalIgnoreCase);
        }
    }

}
