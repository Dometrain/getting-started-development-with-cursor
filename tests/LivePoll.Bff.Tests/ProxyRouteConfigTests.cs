using Microsoft.Extensions.DependencyInjection;

using Yarp.ReverseProxy.Configuration;

namespace LivePoll.Bff.Tests;

public sealed class ProxyRouteConfigTests : IClassFixture<BffWebApplicationFactory>
{
    private readonly BffWebApplicationFactory factory;

    public ProxyRouteConfigTests(BffWebApplicationFactory factory)
    {
        this.factory = factory;
        _ = factory.Services;
    }

    [Fact]
    public void ApiReplicas_AreSeparateDestinations_SessionAffinityOff_WorkerExcluded()
    {
        var config = GetProxyConfig();
        var apiCluster = Assert.Single(config.Clusters, cluster => cluster.ClusterId == "api");
        Assert.NotNull(apiCluster.Destinations);

        Assert.Equal(2, apiCluster.Destinations.Count);
        Assert.Contains("api1", apiCluster.Destinations.Keys);
        Assert.Contains("api2", apiCluster.Destinations.Keys);
        Assert.NotEqual(
            apiCluster.Destinations["api1"].Address,
            apiCluster.Destinations["api2"].Address);
        Assert.True(apiCluster.SessionAffinity is null || apiCluster.SessionAffinity.Enabled is not true);

        Assert.DoesNotContain(
            config.Clusters.SelectMany(cluster => cluster.Destinations ?? new Dictionary<string, DestinationConfig>()),
            destination => destination.Key.Contains("worker", StringComparison.OrdinalIgnoreCase)
                || (destination.Value.Address?.Contains("worker", StringComparison.OrdinalIgnoreCase) ?? false));
        Assert.Equal(0, factory.WorkerHits);
    }

    [Fact]
    public void SimulationsPath_RoutesToSimulator_ApiIsNotDestination()
    {
        var config = GetProxyConfig();
        var simulationsRoutes = config.Routes
            .Where(route => route.Match.Path is not null
                && route.Match.Path.Contains("/admin/simulations", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(simulationsRoutes);
        Assert.All(simulationsRoutes, route => Assert.Equal("simulator", route.ClusterId));

        var simulatorCluster = Assert.Single(config.Clusters, cluster => cluster.ClusterId == "simulator");
        Assert.NotNull(simulatorCluster.Destinations);
        Assert.Contains("simulator", simulatorCluster.Destinations.Keys);
        Assert.DoesNotContain(
            simulatorCluster.Destinations.Keys,
            key => key.Contains("api", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResultsRoute_IsUnbuffered_WithTimeoutAtLeastOneHour()
    {
        var config = GetProxyConfig();
        var resultsRoute = Assert.Single(
            config.Routes,
            route => route.Match.Path is not null
                && route.Match.Path.Contains("/results/stream", StringComparison.OrdinalIgnoreCase));

        var resultsCluster = Assert.Single(
            config.Clusters,
            cluster => cluster.ClusterId == resultsRoute.ClusterId);

        Assert.NotNull(resultsCluster.HttpRequest);
        Assert.Equal(false, resultsCluster.HttpRequest.AllowResponseBuffering);
        Assert.NotNull(resultsCluster.HttpRequest.ActivityTimeout);
        Assert.True(resultsCluster.HttpRequest.ActivityTimeout >= TimeSpan.FromHours(1));
    }

    private IProxyConfig GetProxyConfig()
    {
        var provider = factory.Services.GetRequiredService<IProxyConfigProvider>();
        return provider.GetConfig();
    }
}