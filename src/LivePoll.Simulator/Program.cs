using System.Text.Json.Serialization;

using LivePoll.Simulator;

var builder = WebApplication.CreateBuilder(args);

var publicApi = builder.Configuration["PublicApi:BaseAddress"]
    ?? throw new InvalidOperationException("PublicApi:BaseAddress is missing.");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
var runner = new SimulationRunner(new Uri(publicApi));
builder.Services.AddSingleton(runner);
builder.Services.AddSingleton<ISimulationRunner>(runner);
builder.Services.AddSingleton<SimulationCatalog>();

var app = builder.Build();
app.MapSimulationEndpoints();
app.Run();

public partial class Program;