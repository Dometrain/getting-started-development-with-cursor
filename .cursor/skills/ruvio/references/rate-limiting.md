# Rate limiting

- Package `Ruvio.AspNetCore.RateLimiting`. It is middleware over Ruvio `LIMIT`, not the in-process ASP.NET Core `RateLimiter`. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Fixed window is the default and is first-request-anchored. A rejected request is answered at once with 429. There is no waiting queue. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- The package targets .NET 8. This project uses that .NET 8 package. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Register `AddRuvioClient` first. This middleware does not own or dispose the connection. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- `PartitionKey` is required when a limit has no key of its own. A missing partition is a configuration error. Partition values must be trusted and bounded. The partition and policy are hashed; raw identifiers are not stored in keys. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Call `UseRuvioRateLimiting` after `UseRouting`, `UseAuthentication`, and `UseAuthorization`. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- With no `DefaultPolicy` and no policy on an endpoint, nothing is limited. A 404 is never limited. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- `SlidingWindow` weighs the previous window. `TokenBucket` refills over the window. Both need a server with those `LIMIT` modes. A policy keeps the algorithm it was created with. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Several limits on one policy each send one Ruvio command. They are not atomic: a later refusal has already used an earlier permit. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- `Concurrency` takes a `SEMAPHORE` slot before the endpoint and releases it afterwards, including when the endpoint throws. The slot expires after `maxRequestDuration` (30 seconds by default). https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Default outage mode is `Throw`. `FailOpen` lets requests through unlimited while Ruvio is away. `FailClosed` answers 503. The middleware never retries a possibly-applied `LIMIT`. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Eviction, data loss, deletion, and failover can reset a budget. This is not a billing ledger. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- `LIMIT` returns the remaining allowance, or a null bulk when the window is full, and does not increment on rejection. A key keeps the mode it was created with. `GET` of a limit key is `WRONGTYPE`. https://ruviodb.com/llms/commands.txt
- `SLIDING` keeps a weighted count (maximum up to 2147483647). `TOKENS` capacity is up to 1000000. `WITHRETRY` returns `[remaining, retry_after_ms]`, with remaining `-1` when refused. https://ruviodb.com/llms/commands.txt

```csharp
builder.Services.AddRuvioClient(builder.Configuration.GetSection("Ruvio"));
builder.Services.AddRuvioRateLimiting(options =>
{
    options.KeyPrefix = "orders:rate:v1:";
    options.PartitionKey = context =>
        context.User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("An authenticated tenant is required.");
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRuvioRateLimiting();
app.MapPost("/orders", HandleOrder)
    .RequireAuthorization()
    .WithRuvioRateLimit("create-order", permits: 100, window: TimeSpan.FromMinutes(1));
```

https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt

```csharp
options.AddPolicy("search", permits: 30, window: TimeSpan.FromSeconds(10), RuvioRateLimitAlgorithm.SlidingWindow);
```

https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt

```csharp
options.Outage.Mode = RuvioOutageMode.FailOpen;
```

https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt

- `ClientKey` selects a client registered with `AddKeyedRuvioClient`. Null uses the unkeyed client. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
- Host start fails when `AddRuvioRateLimiting` was not called, the named client is missing, or an endpoint names an unknown policy. The startup check does not open a connection. https://ruviodb.com/llms/integrations/dotnet/rate-limiting.txt
