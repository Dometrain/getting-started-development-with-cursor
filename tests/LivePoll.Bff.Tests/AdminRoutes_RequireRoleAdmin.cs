using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace LivePoll.Bff.Tests;

public sealed class AdminRoutes_RequireRoleAdmin : IClassFixture<BffWebApplicationFactory>
{
    private readonly BffWebApplicationFactory factory;

    public AdminRoutes_RequireRoleAdmin(BffWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Forbidden_UnlessTokenHasRoleAdmin()
    {
        using var client = factory.CreateClient();

        factory.ApiHits = 0;
        factory.SimulatorHits = 0;

        using var missingRoleRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/admin/simulations/run");
        missingRoleRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(role: null));

        using var missingRoleResponse = await client.SendAsync(missingRoleRequest);

        Assert.Equal(HttpStatusCode.Forbidden, missingRoleResponse.StatusCode);
        Assert.Equal(0, factory.SimulatorHits);
        Assert.Equal(0, factory.ApiHits);

        using var adminRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/admin/simulations/run");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(role: "admin"));

        using var adminResponse = await client.SendAsync(adminRequest);
        var adminBody = await adminResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal("simulator", adminBody);
        Assert.Equal(1, factory.SimulatorHits);
        Assert.Equal(0, factory.ApiHits);
    }

    private static string CreateToken(string? role)
    {
        var claims = new List<Claim>
        {
            new("sub", "tester"),
            new("name", "tester"),
        };
        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(BffWebApplicationFactory.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: BffWebApplicationFactory.Issuer,
            audience: BffWebApplicationFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}