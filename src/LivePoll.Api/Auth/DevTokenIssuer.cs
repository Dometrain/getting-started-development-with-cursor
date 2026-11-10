using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace LivePoll.Api.Auth;

public sealed class DevTokenIssuer
{
    private readonly AdminUserDirectory adminUsers;
    private readonly string issuer;
    private readonly string audience;
    private readonly SymmetricSecurityKey signingKey;

    public DevTokenIssuer(IConfiguration configuration, AdminUserDirectory adminUsers)
    {
        this.adminUsers = adminUsers;
        issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
        audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is missing.");
        var key = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public DevTokenResult Issue(string username)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, username),
            new("name", username),
        };

        if (adminUsers.IsAdmin(username))
        {
            claims.Add(new Claim("role", "admin"));
        }

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return new DevTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            username);
    }
}

public sealed record DevTokenResult(string Token, string Username);