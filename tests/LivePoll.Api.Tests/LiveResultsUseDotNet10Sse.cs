using System.Net;
using System.Net.ServerSentEvents;
using System.Reflection;

using LivePoll.Api.LiveResults;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LivePoll.Api.Tests;

public sealed class LiveResultsUseDotNet10Sse
{
    [Fact(DisplayName = "LiveResultsUseDotNet10Sse")]
    public void ResultsStream_Mapped_UsesServerSentEventsResult()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var stream = Assert.Single(
            endpoints.OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText == "/polls/{pollId:guid}/results/stream");
        var method = stream.Metadata.GetMetadata<MethodInfo>();
        Assert.NotNull(method);
        Assert.Equal(typeof(ServerSentEventsResult<ResultsSnapshotResponse>), method.ReturnType);

        var produces = Assert.Single(stream.Metadata.OfType<IProducesResponseTypeMetadata>());
        Assert.Equal(StatusCodes.Status200OK, produces.StatusCode);
        Assert.Contains("text/event-stream", produces.ContentTypes);
        Assert.Equal(typeof(SseItem<ResultsSnapshotResponse>), produces.Type);
    }

    [Fact]
    public async Task Host_Started_ServesNeitherWebSocketNorSignalR()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var poll = await ResultsApi.CreatePollAsync(client);

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        Assert.DoesNotContain(
            endpoints.OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText?.Contains("negotiate", StringComparison.OrdinalIgnoreCase) == true);

        using var negotiate = await client.GetAsync("/results/negotiate");
        Assert.Equal(HttpStatusCode.NotFound, negotiate.StatusCode);

        using var upgrade = new HttpRequestMessage(HttpMethod.Get, $"/polls/{poll.Id}/results/stream");
        upgrade.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        upgrade.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        upgrade.Headers.TryAddWithoutValidation("Sec-WebSocket-Version", "13");
        upgrade.Headers.TryAddWithoutValidation("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==");
        using var response = await client.SendAsync(upgrade, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.SwitchingProtocols, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }
}
