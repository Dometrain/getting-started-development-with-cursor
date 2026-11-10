namespace LivePoll.Api.Problems;

public static class ProblemResponses
{
    public static IResult InvalidRequest(string instance, string detail) =>
        Results.Problem(
            type: "/problems/invalid-request",
            title: "Invalid request",
            statusCode: StatusCodes.Status400BadRequest,
            detail: detail,
            instance: instance);

    public static IResult Unauthorized(string instance) =>
        Results.Problem(
            type: "/problems/unauthorized",
            title: "Unauthorized",
            statusCode: StatusCodes.Status401Unauthorized,
            detail: "A Bearer dev token is required.",
            instance: instance);

    public static IResult Forbidden(string instance) =>
        Results.Problem(
            type: "/problems/forbidden",
            title: "Forbidden",
            statusCode: StatusCodes.Status403Forbidden,
            detail: "An admin is required for this action.",
            instance: instance);

    public static IResult NotFound(string instance, string detail) =>
        Results.Problem(
            type: "/problems/not-found",
            title: "Not found",
            statusCode: StatusCodes.Status404NotFound,
            detail: detail,
            instance: instance);

    public static IResult PollHasVotes(string instance) =>
        Results.Problem(
            type: "/problems/poll-has-votes",
            title: "Poll has votes",
            statusCode: StatusCodes.Status409Conflict,
            detail: "The title and options cannot change after the poll has received a vote.",
            instance: instance);

    public static IResult AlreadyVoted(string instance) =>
        Results.Problem(
            type: "/problems/already-voted",
            title: "Already voted",
            statusCode: StatusCodes.Status409Conflict,
            detail: "This username has already voted on this poll.",
            instance: instance);

    public static IResult PollEnded(string instance) =>
        Results.Problem(
            type: "/problems/poll-ended",
            title: "Poll ended",
            statusCode: StatusCodes.Status409Conflict,
            detail: "This poll is closed. It was open for 10 minutes.",
            instance: instance);

    public static IResult RateLimited(string instance) =>
        Results.Problem(
            type: "/problems/rate-limited",
            title: "Too many requests",
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: "This username has used its vote limit for the current window.",
            instance: instance);

    public static IResult Unavailable(string instance) =>
        Results.Problem(
            type: "/problems/unavailable",
            title: "Service unavailable",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            detail: "The vote could not be accepted because Ruvio is unavailable.",
            instance: instance);
}