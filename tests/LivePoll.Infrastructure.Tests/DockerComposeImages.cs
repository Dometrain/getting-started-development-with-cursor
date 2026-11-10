namespace LivePoll.Infrastructure.Tests;

public static class DockerComposeImages
{
    public static string Ruvio { get; } = ReadServiceImage("ruvio");

    private static string ReadServiceImage(string serviceName)
    {
        var composePath = FindComposeFile();
        var lines = File.ReadAllLines(composePath);
        var serviceHeader = $"  {serviceName}:";
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].TrimEnd(), serviceHeader, StringComparison.Ordinal))
            {
                continue;
            }

            for (var lookAhead = index + 1; lookAhead < lines.Length; lookAhead++)
            {
                var line = lines[lookAhead];
                if (line.Length > 0 && line[0] != ' ' && line[0] != '\t' && line[0] != '#')
                {
                    break;
                }

                if (line.StartsWith("    image:", StringComparison.Ordinal))
                {
                    return line["    image:".Length..].Trim();
                }

                if (line.StartsWith("  ", StringComparison.Ordinal)
                    && !line.StartsWith("    ", StringComparison.Ordinal)
                    && lookAhead > index)
                {
                    break;
                }
            }

            break;
        }

        throw new InvalidOperationException(
            $"Service '{serviceName}' image was not found in '{composePath}'.");
    }

    private static string FindComposeFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docker-compose.yml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("docker-compose.yml was not found from the test base directory.");
    }
}