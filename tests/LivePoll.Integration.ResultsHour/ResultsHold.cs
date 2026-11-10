using System.Globalization;

namespace LivePoll.Integration.ResultsHour;

internal static class ResultsHold
{
    private const int defaultMinutes = 3;

    public static TimeSpan Duration()
    {
        var raw = Environment.GetEnvironmentVariable("RESULTS_HOLD_MINUTES");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TimeSpan.FromMinutes(defaultMinutes);
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 1)
        {
            throw new InvalidOperationException("RESULTS_HOLD_MINUTES must be a positive whole number of minutes.");
        }

        return TimeSpan.FromMinutes(minutes);
    }
}