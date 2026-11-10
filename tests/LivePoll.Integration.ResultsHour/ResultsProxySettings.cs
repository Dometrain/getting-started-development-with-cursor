using System.Globalization;
using System.Text.Json;

namespace LivePoll.Integration.ResultsHour;

public sealed class ResultsProxySettings
{
    [Fact]
    public void NginxAndBff_ReadAndActivityTimeouts_AtLeastOneHourAndUnbuffered()
    {
        var location = ResultsLocation(RepoFile.Read("nginx/nginx.conf"));
        var readTimeout = LastDirective(location, "proxy_read_timeout");
        var (activityTimeout, responseBuffering) = BffResultsRequest(RepoFile.Read("src/LivePoll.Bff/appsettings.json"));

        Assert.True(readTimeout >= TimeSpan.FromHours(1), $"nginx proxy_read_timeout is {readTimeout}.");
        Assert.True(activityTimeout >= TimeSpan.FromHours(1), $"BFF ActivityTimeout is {activityTimeout}.");
        Assert.Equal("off", LastDirectiveValue(location, "proxy_buffering"));
        Assert.False(responseBuffering);
    }

    private static string ResultsLocation(string nginx)
    {
        var text = nginx.Replace("\r\n", "\n", StringComparison.Ordinal);
        var marker = text.IndexOf("results/stream", StringComparison.Ordinal);
        Assert.True(marker >= 0, "nginx.conf has no results/stream location.");
        var open = text.IndexOf('{', marker);
        Assert.True(open >= 0, "results/stream location has no body.");
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == '{')
            {
                depth++;
            }
            else if (text[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text[marker..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException("results/stream location did not close.");
    }

    private static TimeSpan LastDirective(string block, string name)
    {
        var value = LastDirectiveValue(block, name);
        Assert.False(string.IsNullOrWhiteSpace(value), $"{name} is missing from the results location.");
        return ParseNginxTime(value);
    }

    private static string LastDirectiveValue(string block, string name)
    {
        string? value = null;
        foreach (var raw in block.Split('\n'))
        {
            var line = raw.Trim().TrimEnd(';').Trim();
            if (!line.StartsWith(name, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = line[name.Length..].TrimStart();
            if (rest.Length == 0 || !char.IsWhiteSpace(line[name.Length]))
            {
                continue;
            }

            value = rest;
        }

        return value ?? "";
    }

    private static TimeSpan ParseNginxTime(string value)
    {
        var unit = value[^1];
        var digits = value;
        if (unit is 's' or 'm' or 'h' or 'd')
        {
            digits = value[..^1];
        }
        else
        {
            unit = 's';
        }

        var number = int.Parse(digits, CultureInfo.InvariantCulture);
        return unit switch
        {
            's' => TimeSpan.FromSeconds(number),
            'm' => TimeSpan.FromMinutes(number),
            'h' => TimeSpan.FromHours(number),
            'd' => TimeSpan.FromDays(number),
            _ => throw new InvalidOperationException($"Unsupported nginx time '{value}'."),
        };
    }

    private static (TimeSpan ActivityTimeout, bool ResponseBuffering) BffResultsRequest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var proxy = document.RootElement.GetProperty("ReverseProxy");
        var clusterId = ResultsClusterId(proxy);
        var http = proxy.GetProperty("Clusters").GetProperty(clusterId).GetProperty("HttpRequest");
        var activityText = http.GetProperty("ActivityTimeout").GetString();
        if (string.IsNullOrWhiteSpace(activityText))
        {
            throw new InvalidOperationException("ActivityTimeout is missing.");
        }

        return (
            TimeSpan.Parse(activityText, CultureInfo.InvariantCulture),
            http.GetProperty("AllowResponseBuffering").GetBoolean());
    }

    private static string ResultsClusterId(JsonElement proxy)
    {
        foreach (var route in proxy.GetProperty("Routes").EnumerateObject())
        {
            if (!route.Value.TryGetProperty("Match", out var match)
                || !match.TryGetProperty("Path", out var pathElement))
            {
                continue;
            }

            var path = pathElement.GetString();
            if (path is null || !path.Contains("/results/stream", StringComparison.Ordinal))
            {
                continue;
            }

            var clusterId = route.Value.GetProperty("ClusterId").GetString();
            if (clusterId is not { Length: > 0 })
            {
                throw new InvalidOperationException("Results route has no cluster.");
            }

            return clusterId;
        }

        throw new InvalidOperationException("No BFF route matches /results/stream.");
    }
}