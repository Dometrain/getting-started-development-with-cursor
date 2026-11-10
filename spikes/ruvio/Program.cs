using Ruvio.Client;

await using var connection = await RuvioConnection.ConnectAsync(new RuvioClientOptions
{
    Host = "localhost",
    Port = 6379,
    Password = "change-me",
});

IRuvioClient db = await connection.GetDefaultDatabaseAsync();

var allowed = 0;
for (var i = 0; i < 15; i++)
{
    RateLimitResult decision = await db.RateLimitAsync("alice", max: 10, TimeSpan.FromSeconds(1));
    if (decision.Allowed)
        allowed++;
}

Console.WriteLine($"allowed {allowed} of 15");

await using RuvioLease? lease = await db.TryAcquireLeaseAsync("aggregator", TimeSpan.FromSeconds(30));
if (lease is null)
{
    Console.WriteLine("lease acquire failed");
    return;
}

Console.WriteLine($"fencing token {lease.Token}");

await using RuvioLease? again = await db.TryAcquireLeaseAsync("aggregator", TimeSpan.FromSeconds(30));
Console.WriteLine(again is null ? "second acquire failed" : "second acquire succeeded");

bool released = await lease.ReleaseAsync();
Console.WriteLine(released ? "released" : "release failed");
