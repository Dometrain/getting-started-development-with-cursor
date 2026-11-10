using LivePoll.Api.Problems;
using LivePoll.Domain;

namespace LivePoll.Api.Polls;

public static class PollExceptionMapper
{
    private static readonly Action<ILogger, string, Exception?> requestFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "PollRequestFailed"),
            "Request {Instance} failed.");

    public static IResult Map(Exception exception, string instance, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        requestFailed(logger, instance, exception);
        return Map(exception, instance);
    }

    public static IResult Map(Exception exception, string instance)
    {
        if (exception is Application.ApplicationException applicationException)
        {
            if (applicationException.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return ProblemResponses.NotFound(instance, "No poll exists with this id.");
            }

            return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
        }

        if (exception is DomainException domainException)
        {
            if (domainException.Message.Contains("has votes", StringComparison.Ordinal))
            {
                return ProblemResponses.PollHasVotes(instance);
            }

            if (domainException.Message.Contains("Only an admin", StringComparison.Ordinal))
            {
                return ProblemResponses.Forbidden(instance);
            }

            if (domainException.Message.Contains("Option was not found", StringComparison.Ordinal))
            {
                return ProblemResponses.NotFound(instance, "This poll has no option with this id.");
            }

            return ProblemResponses.InvalidRequest(instance, "The request body does not match the contract.");
        }

        throw exception;
    }
}