using System.Runtime.CompilerServices;

using LivePoll.Api.Problems;
using LivePoll.Application;

using Microsoft.AspNetCore.Http.HttpResults;

namespace LivePoll.Api.LiveResults;

public static class ResultsEndpoints
{
    public static IEndpointRouteBuilder MapResultsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/polls/{pollId:guid}/results", GetResults);
        app.MapGet("/polls/{pollId:guid}/results/stream", StreamResults)
            .AddEndpointFilter(RequireExistingPollAsync);
        return app;
    }

    private static async Task<IResult> GetResults(
        Guid pollId,
        CurrentResultsReader reader,
        CancellationToken cancellationToken)
    {
        var snapshot = await reader.ReadAsync(pollId, cancellationToken);
        if (snapshot is null)
        {
            return ProblemResponses.NotFound($"/polls/{pollId}/results", "No poll exists with this id.");
        }

        return Results.Ok(snapshot);
    }

    private static ServerSentEventsResult<ResultsSnapshotResponse> StreamResults(
        Guid pollId,
        ResultsSnapshotHub hub,
        CurrentResultsReader reader,
        CancellationToken cancellationToken) =>
        TypedResults.ServerSentEvents(ForwardAsync(pollId, hub, reader, cancellationToken), "results");

    private static async IAsyncEnumerable<ResultsSnapshotResponse> ForwardAsync(
        Guid pollId,
        ResultsSnapshotHub hub,
        CurrentResultsReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var viewer = hub.Subscribe(pollId);
        try
        {
            var current = await reader.ReadAsync(pollId, cancellationToken);
            if (current is null)
            {
                yield break;
            }

            yield return current;

            await foreach (var snapshot in viewer.Reader.ReadAllAsync(cancellationToken))
            {
                yield return snapshot.ToResponse();
            }
        }
        finally
        {
            hub.Unsubscribe(viewer);
        }
    }

    private static async ValueTask<object?> RequireExistingPollAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!TryReadPollId(context, out var pollId))
        {
            return ProblemResponses.InvalidRequest(
                context.HttpContext.Request.Path,
                "The request does not match the contract.");
        }

        var polls = context.HttpContext.RequestServices.GetRequiredService<IPollRepository>();
        var stored = await polls.GetAsync(pollId, context.HttpContext.RequestAborted);
        if (stored is null)
        {
            return ProblemResponses.NotFound(
                $"/polls/{pollId}/results/stream",
                "No poll exists with this id.");
        }

        return await next(context);
    }

    private static bool TryReadPollId(EndpointFilterInvocationContext context, out Guid pollId)
    {
        var value = context.HttpContext.Request.RouteValues["pollId"]?.ToString();
        return Guid.TryParse(value, out pollId);
    }
}
