using System.Security.Claims;

using LivePoll.Api.Problems;
using LivePoll.Application;

using Ruvio.AspNetCore.RateLimiting;

namespace LivePoll.Api.Votes;

public static class VoteEndpoints
{
    public const string RateLimitPolicyName = "vote";

    public static RouteGroupBuilder MapVoteEndpoints(
        this WebApplication app,
        int permits,
        TimeSpan window)
    {
        var group = app.MapGroup("/polls");
        group.MapPost("/{pollId:guid}/votes", CastVote)
            .RequireAuthorization()
            .WithRuvioRateLimit(RateLimitPolicyName, permits, window);
        return group;
    }

    private static async Task<IResult> CastVote(
        Guid pollId,
        CastVoteRequest? request,
        ClaimsPrincipal user,
        VoteAcceptanceService votes,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var instance = $"/polls/{pollId}/votes";
        if (request?.OptionId is null || request.OptionId == Guid.Empty)
        {
            return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
        }

        var username = user.FindFirstValue("name")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(username))
        {
            return ProblemResponses.Unauthorized(instance);
        }

        try
        {
            var accepted = await votes.AcceptAsync(
                pollId,
                username,
                request.OptionId.Value,
                cancellationToken);
            return Results.Accepted(
                instance,
                new VoteAcceptedResponse(accepted.PollId, accepted.OptionId, accepted.Username));
        }
        catch (Exception exception) when (exception is Application.ApplicationException)
        {
            return VoteExceptionMapper.Map(exception, instance, loggerFactory.CreateLogger(nameof(VoteEndpoints)));
        }
    }
}