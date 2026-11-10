using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LivePoll.Api.Tests;

public sealed class StatelessTokenAcceptedTests
{
    private static readonly string[] twoOptions = ["Cafeteria", "Food truck"];

    [Fact]
    public async Task SameToken_AcceptedOnNewHost_WithNoServerSession()
    {
        string token;
        await using (var issuer = new ApiWebApplicationFactory())
        {
            using var signInClient = issuer.CreateClient();
            using var signIn = await signInClient.PostAsJsonAsync(
                "/auth/dev-token",
                new { username = "ada" });
            signIn.EnsureSuccessStatusCode();
            var body = await signIn.Content.ReadFromJsonAsync<DevTokenBody>();
            Assert.NotNull(body);
            token = body.Token;
        }

        await using var consumer = new ApiWebApplicationFactory();
        using var client = consumer.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/polls",
            new { title = "Lunch spot", options = twoOptions });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record DevTokenBody(string Token, string Username);
}