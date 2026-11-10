using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LivePoll.Api.Tests;

public sealed class TokenFromUserNameOnly : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public TokenFromUserNameOnly(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task SignIn_WithUsernameOnly_ReturnsSignedStatelessJwt()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username = "ada" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);
        Assert.Equal("ada", body.Username);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.Equal("ada", jwt.Subject);
        Assert.Contains(jwt.Claims, claim => claim.Type == "name" && claim.Value == "ada");
        Assert.Equal(ApiWebApplicationFactory.Issuer, jwt.Issuer);
        Assert.Contains(ApiWebApplicationFactory.Audience, jwt.Audiences);
        Assert.NotNull(jwt.RawSignature);
        Assert.NotEmpty(jwt.RawSignature);
    }

    [Fact]
    public async Task SignIn_BlankUsername_Returns400ProblemJson()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        Assert.Equal("/problems/invalid-request", root.GetProperty("type").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
    }

    private sealed record DevTokenBody(string Token, string Username);
}
