using LivePoll.Application;
using LivePoll.Infrastructure;
using LivePoll.Infrastructure.Ruvio;
using LivePoll.Worker;
using LivePoll.Worker.Voting;

using Ruvio.Client;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
}

builder.Services.AddInfrastructure(connectionString);

var ruvioOptions = CreateRuvioOptions(builder.Configuration);
var commandsConnection = await RuvioConnection.ConnectAsync(ruvioOptions);
var blockingConnection = await RuvioConnection.ConnectAsync(ruvioOptions);
var commandsClient = await commandsConnection.GetDefaultDatabaseAsync();
var blockingReadsClient = await blockingConnection.GetDefaultDatabaseAsync();

builder.Services.AddSingleton(commandsConnection);
builder.Services.AddSingleton(blockingConnection);
builder.Services.AddSingleton<IRuvioClient>(commandsClient);
builder.Services.AddSingleton(new RuvioVoteDeliverySource(commandsClient, blockingReadsClient));
builder.Services.AddSingleton<IVoteDeliverySource>(static sp => sp.GetRequiredService<RuvioVoteDeliverySource>());
builder.Services.AddSingleton(new RuvioVoteCounters(commandsClient));
builder.Services.AddSingleton<IBacklogCounter, RuvioBacklogCounter>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IBusinessMetricsStore, RuvioBusinessMetricsStore>();
builder.Services.AddSingleton<IPollOptionSource, PostgresPollOptionSource>();
builder.Services.AddSingleton<IPublishLock, RuvioPublishLock>();
builder.Services.AddSingleton<ILiveTotals, RuvioLiveTotals>();
builder.Services.AddSingleton<IActivePollCleaner, RuvioActivePollCleaner>();
builder.Services.AddSingleton<RuvioLiveSnapshots>();
builder.Services.AddSingleton<IResultsPublisher, RuvioResultsPublisher>();
builder.Services.AddSingleton<ISnapshotFence, RuvioSnapshotFence>();
builder.Services.AddSingleton<SnapshotPublisher>();
builder.Services.AddScoped<IVoteStore, PostgresVoteStore>();
builder.Services.AddScoped<VoteStoreProcessor>();
builder.Services.AddHostedService<SnapshotPublishService>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();

static RuvioClientOptions CreateRuvioOptions(IConfiguration configuration)
{
    var ruvio = configuration.GetSection("Ruvio");
    var host = ruvio["Host"];
    var options = new RuvioClientOptions
    {
        Host = string.IsNullOrWhiteSpace(host) ? "ruvio" : host,
        Port = int.TryParse(ruvio["Port"], out var port) ? port : 6379,
    };
    var password = ruvio["Password"];
    if (!string.IsNullOrEmpty(password))
    {
        options.Password = password;
    }

    return options;
}