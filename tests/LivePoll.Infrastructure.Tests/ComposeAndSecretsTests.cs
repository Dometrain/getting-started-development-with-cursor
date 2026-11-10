using System.Text.RegularExpressions;

namespace LivePoll.Infrastructure.Tests;

public sealed class ComposeAndSecretsTests
{
    private static readonly string[] rootCaptures =
    [
        "vote.json",
        "vote-body.json",
        "signin.json",
        "poll-req.json",
        "poll-body.json",
        "poll-headers.txt",
        "token-req.json",
        "results1.json",
        "results2.json",
        "stream-headers.txt",
        "stream-body.txt",
        "stream-watch.txt",
    ];

    [Fact]
    public void Compose_EveryInterpolationHasADefault()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var matches = Regex.Matches(compose, @"\$\{(?<name>[A-Za-z0-9_]+)(?<default>:[^}]*)?\}");

        Assert.NotEmpty(matches);
        Assert.All(matches, match => Assert.True(match.Groups["default"].Success, match.Value));
    }

    [Fact]
    public void Compose_WorkerReceivesPostgresAndRuvio()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var worker = ServiceBlock(compose, "worker1");
        Assert.Contains("ConnectionStrings__DefaultConnection:", worker, StringComparison.Ordinal);
        Assert.Contains("Ruvio__Host:", worker, StringComparison.Ordinal);
        Assert.Contains("Ruvio__Password:", worker, StringComparison.Ordinal);
        Assert.Contains("${RUVIO_REQUIREPASS:-livepoll}", worker, StringComparison.Ordinal);
        Assert.Contains("${POSTGRES_PASSWORD:-livepoll}", worker, StringComparison.Ordinal);
        Assert.Contains("<<: *worker", ServiceBlock(compose, "worker2"), StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_WorkersUseHostnamesWorker1AndWorker2()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        Assert.Contains("hostname: worker1", ServiceBlock(compose, "worker1"), StringComparison.Ordinal);
        Assert.Contains("hostname: worker2", ServiceBlock(compose, "worker2"), StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_ApiReceivesAdminUsersAndSigningKey()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        const string signingKey = "${JWT_SIGNING_KEY:-livepoll-dev-signing-key-32bytes!}";
        var api = ServiceBlock(compose, "api1");
        Assert.Contains("Admin__Users:", api, StringComparison.Ordinal);
        Assert.Contains("${ADMIN_USERS:-ada-admin}", api, StringComparison.Ordinal);
        Assert.Contains("Jwt__SigningKey:", api, StringComparison.Ordinal);
        Assert.Contains(signingKey, api, StringComparison.Ordinal);
        Assert.Contains("<<: *api", ServiceBlock(compose, "api2"), StringComparison.Ordinal);

        Assert.Contains(signingKey, ServiceBlock(compose, "bff"), StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_OnlyNginxPublishesAHostPort()
    {
        var compose = ReadRepoFile("docker-compose.yml").Replace("\r\n", "\n", StringComparison.Ordinal);
        var publishers = new List<string>();
        string? service = null;
        foreach (var line in compose.Split('\n'))
        {
            if (line.Length > 2
                && line[0] == ' '
                && line[1] == ' '
                && line[2] != ' '
                && line.EndsWith(':'))
            {
                service = line.Trim()[..^1];
            }

            if (service is not null && line == "    ports:")
            {
                publishers.Add(service);
            }
        }

        Assert.Equal("nginx", Assert.Single(publishers));
    }

    [Fact]
    public void AppSettings_DoNotContainSecrets()
    {
        var files = new[]
        {
            "src/LivePoll.Api/appsettings.json",
            "src/LivePoll.Api/appsettings.Development.json",
            "src/LivePoll.Worker/appsettings.json",
            "src/LivePoll.Worker/appsettings.Development.json",
            "src/LivePoll.Bff/appsettings.json",
            "src/LivePoll.Bff/appsettings.Development.json",
            "src/LivePoll.Infrastructure/Persistence/LivePollDbContextFactory.cs",
            "deploy/postgres/01-readonly.sh",
        };

        foreach (var relative in files)
        {
            var text = ReadRepoFile(relative);
            Assert.DoesNotContain("Password=livepoll", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Password\": \"livepoll\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Password\": \"change-me\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("livepoll-dev-signing-key", text, StringComparison.Ordinal);
            Assert.DoesNotContain("livepoll_readonly", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReadonlyInit_UsesTheComposeDatabase()
    {
        var compose = ReadRepoFile("docker-compose.yml");
        var script = ReadRepoFile("deploy/postgres/01-readonly.sh");

        Assert.Contains("01-readonly.sh", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("01-readonly.sql", compose, StringComparison.Ordinal);
        Assert.Contains("LIVEPOLL_READONLY_PASSWORD", script, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_DB", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DATABASE livepoll", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RootCaptures_AreNotInTheRepo()
    {
        var root = RepoRoot.Find();

        Assert.All(
            rootCaptures,
            name => Assert.False(File.Exists(Path.Combine(root, name)), name));
    }

    private static string ReadRepoFile(string relative)
    {
        var path = Path.Combine(RepoRoot.Find(), relative.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path);
    }

    private static string ServiceBlock(string compose, string service)
    {
        var lines = compose.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var header = $"  {service}:";
        var start = Array.FindIndex(
            lines,
            line => line.StartsWith(header, StringComparison.Ordinal)
                && (line.Length == header.Length || line[header.Length] == ' '));
        Assert.True(start >= 0, service);
        var end = start + 1;
        while (end < lines.Length)
        {
            var line = lines[end];
            if (line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ')
            {
                break;
            }

            end++;
        }

        return string.Join('\n', lines[start..end]);
    }
}