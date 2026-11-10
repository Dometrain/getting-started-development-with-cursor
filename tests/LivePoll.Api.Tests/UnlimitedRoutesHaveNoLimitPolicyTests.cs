using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Ruvio.AspNetCore.RateLimiting;

namespace LivePoll.Api.Tests;

public sealed class UnlimitedRoutesHaveNoLimitPolicyTests
{
    [Fact]
    public void SignInAndResultsStream_HaveNoLimitPolicy()
    {
        using var factory = new ApiWebApplicationFactory();
        using var _ = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<RuvioRateLimitingOptions>>();
        Assert.Null(options.Value.DefaultPolicy);

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var signIn = Assert.Single(
            endpoints.OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText?.Contains("auth/dev-token", StringComparison.Ordinal) == true);
        Assert.Empty(signIn.Metadata.OfType<RuvioRateLimitAttribute>());

        var stream = Assert.Single(
            endpoints.OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText?.Contains("results/stream", StringComparison.Ordinal) == true);
        Assert.Empty(stream.Metadata.OfType<RuvioRateLimitAttribute>());

        var vote = Assert.Single(
            endpoints.OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText?.Contains("votes", StringComparison.Ordinal) == true);
        Assert.Contains(vote.Metadata.OfType<RuvioRateLimitAttribute>(), attribute => attribute.Policy == "vote");
    }
}
