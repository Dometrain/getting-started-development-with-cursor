using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;

namespace LivePoll.Api.Tests;

public sealed class AdminUsers_ReceiveRoleAdmin : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory factory;

    public AdminUsers_ReceiveRoleAdmin(ApiWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task SignIn_NameInAdminUsers_TokenHasRoleAdmin()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username = ApiWebApplicationFactory.AdminUser });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.Contains(jwt.Claims, claim => claim.Type == "role" && claim.Value == "admin");
    }

    [Fact]
    public async Task SignIn_NameNotInAdminUsers_TokenHasNoRoleAdmin()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/dev-token",
            new { username = "ada" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == "role" && claim.Value == "admin");
    }

    private sealed record DevTokenBody(string Token, string Username);
}
