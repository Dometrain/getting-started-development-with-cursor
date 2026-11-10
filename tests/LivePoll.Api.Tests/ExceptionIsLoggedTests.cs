using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LivePoll.Api.Tests;

public sealed class ExceptionIsLoggedTests : IClassFixture<LoggingApiFactory>
{
    private static readonly string[] twoOptions = ["Cafeteria", "Food truck"];

    private readonly LoggingApiFactory factory;

    public ExceptionIsLoggedTests(LoggingApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task BlankTitle_IsLoggedAndDetailOmitsTheExceptionMessage()
    {
        using var client = factory.CreateClient();
        var token = await SignInAsync(client, "ada");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(
            "/polls",
            new { title = " ", options = twoOptions });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var detail = document.RootElement.GetProperty("detail").GetString();
        Assert.Equal("The request body does not match the contract.", detail);
        Assert.DoesNotContain("Title cannot be blank", detail, StringComparison.Ordinal);
        Assert.Contains(factory.Logs.Entries, entry => entry.Exception is LivePoll.Domain.DomainException);
    }

    [Fact]
    public async Task UnmappedVoteFailure_IsLoggedAndOmittedFromTheResponse()
    {
        var logger = new RecordingLogger();
        var result = LivePoll.Api.Votes.VoteExceptionMapper.Map(
            new LivePoll.Application.ApplicationException("database password=secret"),
            "/polls/x/votes",
            logger);

        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        Assert.DoesNotContain("password=secret", body, StringComparison.Ordinal);
        Assert.Contains("The request body does not match the contract.", body, StringComparison.Ordinal);
        Assert.Contains(logger.Entries, entry => entry.Exception is LivePoll.Application.ApplicationException);
    }

    private static async Task<string> SignInAsync(HttpClient client, string username)
    {
        using var response = await client.PostAsJsonAsync("/auth/dev-token", new { username });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevTokenBody>();
        Assert.NotNull(body);
        return body.Token;
    }

    private sealed record DevTokenBody(string Token, string Username);
}

public sealed class LoggingApiFactory : ApiWebApplicationFactory
{
    public RecordingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(Logs);
        });
    }
}

public sealed class RecordingLoggerProvider : ILoggerProvider
{
    public List<LogCapture> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

    public void Dispose()
    {
    }
}

public sealed class RecordingLogger : ILogger
{
    private readonly List<LogCapture> entries;

    public RecordingLogger()
        : this([])
    {
    }

    public RecordingLogger(List<LogCapture> entries)
    {
        this.entries = entries;
    }

    public List<LogCapture> Entries => entries;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => EmptyScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (entries)
        {
            entries.Add(new LogCapture(formatter(state, exception), exception));
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public sealed record LogCapture(string Message, Exception? Exception);