using LivePoll.Api.Problems;

namespace LivePoll.Api.Votes;

public static class VoteExceptionMapper
{
    private static readonly Action<ILogger, string, Exception?> requestFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "VoteRequestFailed"),
            "Request {Instance} failed.");

    public static IResult Map(Exception exception, string instance, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        requestFailed(logger, instance, exception);
        return Map(exception, instance);
    }

    public static IResult Map(Exception exception, string instance)
    {
        if (exception is not Application.ApplicationException applicationException)
        {
            throw exception;
        }

        var message = applicationException.Message;
        if (message.Contains("already voted", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemResponses.AlreadyVoted(instance);
        }

        if (message.Contains("closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("poll is closed", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemResponses.PollEnded(instance);
        }

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemResponses.NotFound(instance, "No poll exists with this id.");
        }

        if (message.Contains("signed-in", StringComparison.OrdinalIgnoreCase))
        {
            return ProblemResponses.Unauthorized(instance);
        }

        return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
    }
}