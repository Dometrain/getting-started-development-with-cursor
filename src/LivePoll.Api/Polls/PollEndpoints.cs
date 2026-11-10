using System.Security.Claims;

using LivePoll.Api.Problems;
using LivePoll.Application;
using LivePoll.Domain;

namespace LivePoll.Api.Polls;

public static class PollEndpoints
{
    public static RouteGroupBuilder MapPollEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/polls");
        group.MapPost("/", CreatePoll).RequireAuthorization();
        group.MapGet("/{pollId:guid}", GetPoll).RequireAuthorization();
        group.MapPatch("/{pollId:guid}", UpdateTitle).RequireAuthorization("Admin");
        group.MapPost("/{pollId:guid}/options", AddOption).RequireAuthorization("Admin");
        group.MapPatch("/{pollId:guid}/options/{optionId:guid}", EditOption).RequireAuthorization("Admin");
        group.MapDelete("/{pollId:guid}/options/{optionId:guid}", RemoveOption).RequireAuthorization("Admin");
        return group;
    }

    private static async Task<IResult> CreatePoll(
        CreatePollRequest? request,
        ClaimsPrincipal user,
        PollLifecycleService polls,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        const string instance = "/polls";
        if (request is null)
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
            var view = await polls.CreateAsync(
                username,
                request.Title ?? string.Empty,
                request.Options ?? [],
                cancellationToken);
            var body = PollResponseMapper.FromView(view);
            return Results.Created($"/polls/{body.Id}", body);
        }
        catch (Exception exception) when (exception is DomainException or Application.ApplicationException)
        {
            return PollExceptionMapper.Map(exception, instance, loggerFactory.CreateLogger(nameof(PollEndpoints)));
        }
    }

    private static async Task<IResult> GetPoll(
        Guid pollId,
        IPollRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var instance = $"/polls/{pollId}";
        var stored = await repository.GetAsync(pollId, cancellationToken);
        if (stored is null)
        {
            return ProblemResponses.NotFound(instance, "No poll exists with this id.");
        }

        return Results.Ok(PollResponseMapper.FromStored(stored, clock));
    }

    private static async Task<IResult> UpdateTitle(
        Guid pollId,
        UpdatePollTitleRequest? request,
        PollLifecycleService polls,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var instance = $"/polls/{pollId}";
        if (request is null)
        {
            return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
        }

        try
        {
            var view = await polls.UpdateTitleAsync(
                pollId,
                request.Title ?? string.Empty,
                ActorRole.Admin,
                cancellationToken);
            return Results.Ok(PollResponseMapper.FromView(view));
        }
        catch (Exception exception) when (exception is DomainException or Application.ApplicationException)
        {
            return PollExceptionMapper.Map(exception, instance, loggerFactory.CreateLogger(nameof(PollEndpoints)));
        }
    }

    private static async Task<IResult> AddOption(
        Guid pollId,
        PollOptionTextRequest? request,
        PollLifecycleService polls,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var instance = $"/polls/{pollId}/options";
        if (request is null)
        {
            return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
        }

        try
        {
            var view = await polls.AddOptionAsync(
                pollId,
                request.Text ?? string.Empty,
                ActorRole.Admin,
                cancellationToken);
            return Results.Ok(PollResponseMapper.FromView(view));
        }
        catch (Exception exception) when (exception is DomainException or Application.ApplicationException)
        {
            return PollExceptionMapper.Map(exception, instance, loggerFactory.CreateLogger(nameof(PollEndpoints)));
        }
    }

    private static async Task<IResult> EditOption(
        Guid pollId,
        Guid optionId,
        PollOptionTextRequest? request,
        PollLifecycleService polls,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var instance = $"/polls/{pollId}/options/{optionId}";
        if (request is null)
        {
            return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
        }

        try
        {
            var view = await polls.EditOptionAsync(
                pollId,
                optionId,
                request.Text ?? string.Empty,
                ActorRole.Admin,
                cancellationToken);
            return Results.Ok(PollResponseMapper.FromView(view));
        }
        catch (Exception exception) when (exception is DomainException or Application.ApplicationException)
        {
            return PollExceptionMapper.Map(exception, instance, loggerFactory.CreateLogger(nameof(PollEndpoints)));
        }
    }

    private static async Task<IResult> RemoveOption(
        Guid pollId,
        Guid optionId,
        PollLifecycleService polls,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var instance = $"/polls/{pollId}/options/{optionId}";
        try
        {
            var view = await polls.RemoveOptionAsync(
                pollId,
                optionId,
                ActorRole.Admin,
                cancellationToken);
            return Results.Ok(PollResponseMapper.FromView(view));
        }
        catch (Exception exception) when (exception is DomainException or Application.ApplicationException)
        {
            return PollExceptionMapper.Map(exception, instance, loggerFactory.CreateLogger(nameof(PollEndpoints)));
        }
    }
}