using LivePoll.Application;

namespace LivePoll.Api.Metrics;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/metrics", GetMetrics).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetMetrics(
        BusinessMetricsService metrics,
        ActivePollOptionLister options,
        CancellationToken cancellationToken)
    {
        var queries = await options.ListAsync(cancellationToken);
        var view = await metrics.GetAsync(queries, cancellationToken);
        var votesPerOption = view.VotesPerOption
            .Select(option => new OptionVoteMetricResponse(option.PollId, option.OptionId, option.Text, option.Votes))
            .ToList();
        return Results.Ok(new BusinessMetricsResponse(
            view.VotesPerSecond,
            view.TotalVotes,
            view.ActivePolls,
            votesPerOption,
            view.VotesWaitingForPostgres));
    }
}
