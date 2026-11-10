using LivePoll.Api.Problems;

namespace LivePoll.Api.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");
        group.MapPost("/dev-token", CreateDevToken);
        return group;
    }

    private static IResult CreateDevToken(DevTokenRequest? request, DevTokenIssuer issuer)
    {
        const string instance = "/auth/dev-token";
        if (request is null || string.IsNullOrWhiteSpace(request.Username))
        {
            return ProblemResponses.InvalidRequest(
                instance,
                "Username is required and must not be blank or whitespace.");
        }

        var token = issuer.Issue(request.Username.Trim());
        return Results.Ok(new { token = token.Token, username = token.Username });
    }
}