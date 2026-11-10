using System.Net;
using System.Net.Http.Json;

namespace LivePoll.Integration.Compose;

[Collection(ComposeGroup.Name)]
public sealed class OnlyNginxIsPublished(ComposeFixture stack)
{
    [Fact(DisplayName = "OnlyNginxIsPublished")]
    public async Task OutsideTheDockerNetwork_OnlyNginxAcceptsAConnection()
    {
        var containers = await stack.ContainersAsync();
        var published = containers.Where(container => container.HostPorts.Count > 0).ToArray();
        var nginx = Assert.Single(published);
        Assert.Equal("nginx", nginx.Service);

        using var client = new HttpClient { BaseAddress = stack.Origin, Timeout = TimeSpan.FromSeconds(30) };
        var html = await client.GetStringAsync("/");
        Assert.Contains("LivePoll", html, StringComparison.Ordinal);
        Assert.Contains("id=\"root\"", html, StringComparison.Ordinal);
        Assert.Contains("/assets/", html, StringComparison.Ordinal);

        using var apiCall = await client.PostAsJsonAsync("/admin/simulations", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, apiCall.StatusCode);
    }
}