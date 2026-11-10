using LivePoll.Api.Problems;

using Ruvio.AspNetCore.RateLimiting;
using Ruvio.Client;

namespace LivePoll.Api.RateLimiting;

public static class VoteRateLimitingExtensions
{
    public const string PermitsKey = "VoteRateLimit:Permits";
    public const string WindowSecondsKey = "VoteRateLimit:WindowSeconds";

    public static IServiceCollection AddLivePollVoteRateLimiting(this IServiceCollection services)
    {
        services.AddRuvioRateLimiting(options =>
        {
            options.KeyPrefix = "livepoll:vote:rate:v1:";
            options.PartitionKey = context =>
                context.User.FindFirst("name")?.Value
                ?? throw new InvalidOperationException("An authenticated username is required for vote rate limiting.");
            options.Outage.Mode = RuvioOutageMode.FailClosed;
            options.OnRejected = async (context, _) =>
            {
                var problem = ProblemResponses.RateLimited(context.Request.Path);
                await problem.ExecuteAsync(context);
            };
        });

        return services;
    }

    public static (int Permits, TimeSpan Window) ReadVoteRateLimit(IConfiguration configuration)
    {
        var permits = configuration.GetValue(PermitsKey, 100);
        var windowSeconds = configuration.GetValue(WindowSecondsKey, 1);
        if (permits <= 0)
        {
            throw new InvalidOperationException($"{PermitsKey} must be positive.");
        }

        if (windowSeconds <= 0)
        {
            throw new InvalidOperationException($"{WindowSecondsKey} must be positive.");
        }

        return (permits, TimeSpan.FromSeconds(windowSeconds));
    }
}
