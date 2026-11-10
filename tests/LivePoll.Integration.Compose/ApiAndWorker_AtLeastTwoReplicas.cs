namespace LivePoll.Integration.Compose;

[Collection(ComposeGroup.Name)]
public sealed class ApiAndWorker_AtLeastTwoReplicas(ComposeFixture stack)
{
    [Fact(DisplayName = "ApiAndWorker_AtLeastTwoReplicas")]
    public async Task AfterStartup_AtLeastTwoApiAndTwoWorkerReplicasAreRunning()
    {
        var containers = await stack.ContainersAsync();
        var apis = containers.Where(container => container.Service is "api1" or "api2" && container.State == "running").ToArray();
        var workers = containers.Where(container => container.Service is "worker1" or "worker2" && container.State == "running").ToArray();

        Assert.True(apis.Length >= 2, string.Join(", ", containers.Select(container => container.Service + ":" + container.State)));
        Assert.True(workers.Length >= 2, string.Join(", ", containers.Select(container => container.Service + ":" + container.State)));
    }
}