using LivePoll.Api.Auth;
using LivePoll.Api.LiveResults;
using LivePoll.Api.Metrics;
using LivePoll.Api.Polls;
using LivePoll.Api.RateLimiting;
using LivePoll.Api.Votes;
using LivePoll.Application;
using LivePoll.Infrastructure;
using LivePoll.Infrastructure.Persistence;
using LivePoll.Infrastructure.Ruvio;

using Microsoft.EntityFrameworkCore;

using Ruvio.AspNetCore.RateLimiting;
using Ruvio.Client.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddRuvioClient(builder.Configuration.GetSection("Ruvio"));
builder.Services.AddLivePollVoteRateLimiting();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IPollRepository, PostgresPollRepository>();
builder.Services.AddScoped<PollLifecycleService>();
builder.Services.AddSingleton<RuvioPollEndTimes>();
builder.Services.AddSingleton<IPollEndTimeReader>(static provider => provider.GetRequiredService<RuvioPollEndTimes>());
builder.Services.AddSingleton<IPollEndTimeWriter>(static provider => provider.GetRequiredService<RuvioPollEndTimes>());
builder.Services.AddSingleton<IActivePollWriter, RuvioActivePolls>();
builder.Services.AddSingleton<IOneVoteMarker, RuvioOneVoteMarker>();
builder.Services.AddSingleton<IVoteQueue, RuvioVoteQueue>();
builder.Services.AddSingleton<IAcceptanceMetrics, RuvioVoteCounters>();
builder.Services.AddSingleton<VoteAcceptanceService>();
builder.Services.AddSingleton<IBusinessMetricsStore, RuvioBusinessMetricsStore>();
builder.Services.AddSingleton<BusinessMetricsService>();
builder.Services.AddSingleton<IActivePollDirectory, RuvioActivePollDirectory>();
builder.Services.AddSingleton<ActivePollOptionLister>();
builder.Services.AddSingleton<CurrentResultsReader>();
builder.Services.AddSingleton<ResultsSnapshotHub>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<RuvioResultsListener>();
}

builder.Services.AddSingleton<AdminUserDirectory>();
builder.Services.AddSingleton<DevTokenIssuer>();
builder.Services.AddLivePollJwtAuth(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<LivePollDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
    {
        await DevelopmentSeed.ApplyAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IPollEndTimeWriter>(),
            scope.ServiceProvider.GetRequiredService<IActivePollWriter>());
    }
}

var (votePermits, voteWindow) = VoteRateLimitingExtensions.ReadVoteRateLimit(app.Configuration);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRuvioRateLimiting();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapPollEndpoints();
app.MapVoteEndpoints(votePermits, voteWindow);
app.MapResultsEndpoints();
app.MapMetricsEndpoints();

app.Run();

public partial class Program;