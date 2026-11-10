namespace LivePoll.Simulator;

public static class SimulationEndpoints
{
    public static WebApplication MapSimulationEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/simulations", Start);
        app.MapGet("/admin/simulations/{simulationId:guid}", Get);
        app.MapDelete("/admin/simulations/{simulationId:guid}", Stop);
        return app;
    }

    private static IResult Start(StartSimulationRequest? request, SimulationCatalog catalog)
    {
        const string instance = "/admin/simulations";
        if (request is null || request.PollId == Guid.Empty || request.VoteSplit is null || request.VoteSplit.Count == 0)
        {
            return Invalid(instance);
        }

        try
        {
            var simulation = catalog.Start(request);
            return Results.Created($"/admin/simulations/{simulation.Id}", simulation.ToResponse());
        }
        catch (ArgumentException)
        {
            return Invalid(instance);
        }
    }

    private static IResult Get(Guid simulationId, SimulationCatalog catalog)
    {
        var simulation = catalog.Find(simulationId);
        if (simulation is null)
        {
            return Missing(simulationId);
        }

        return Results.Ok(simulation.ToResponse());
    }

    private static IResult Stop(Guid simulationId, SimulationCatalog catalog)
    {
        var simulation = catalog.Stop(simulationId);
        if (simulation is null)
        {
            return Missing(simulationId);
        }

        return Results.Ok(simulation.ToResponse());
    }

    private static IResult Invalid(string instance) =>
        Results.Problem(
            type: "/problems/invalid-request",
            title: "Invalid request",
            statusCode: StatusCodes.Status400BadRequest,
            detail: "The request body does not match the contract.",
            instance: instance);

    private static IResult Missing(Guid simulationId) =>
        Results.Problem(
            type: "/problems/not-found",
            title: "Not found",
            statusCode: StatusCodes.Status404NotFound,
            detail: "No simulation exists with this id.",
            instance: $"/admin/simulations/{simulationId}");
}