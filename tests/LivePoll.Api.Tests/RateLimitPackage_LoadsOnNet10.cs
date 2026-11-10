using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Ruvio.AspNetCore.RateLimiting;

namespace LivePoll.Api.Tests;

public sealed class RateLimitPackage_LoadsOnNet10
{
    [Fact]
    public void HostStarts_WithRuvioAspNetCoreRateLimitingRegistered()
    {
        using var factory = new ApiWebApplicationFactory();
        using var _ = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<RuvioRateLimitingOptions>>();
        Assert.NotNull(options.Value);
        Assert.Null(options.Value.DefaultPolicy);
        Assert.Contains(
            typeof(RuvioRateLimitingExtensions).Assembly.GetName().Name!,
            AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name));
    }
}
