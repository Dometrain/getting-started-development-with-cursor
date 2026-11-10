# Leases and locks

- Package `Ruvio.AspNetCore.DistributedLock`. It needs an `IRuvioClient` already registered. It does not own that client. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- Default commands: acquire `SET key token NX PX lease`, renew `SET key token IFEQ token PX lease`, release `DELEX key IFEQ token`. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- `SET key value IFEQ expected` writes only when the string equals `expected` and answers nil otherwise. https://ruviodb.com/llms/commands.txt
- `DELEX key IFEQ value` deletes the string only when it equals `value` and returns 1, otherwise 0. https://ruviodb.com/llms/commands.txt
- `Fenced = true` uses `LEASE`, renewed with `RENEW` and freed with `RELEASE`. `FencingToken` is null when `Fenced` is off. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- Turn `Fenced` on for every instance at once. A plain lock and a fenced lock must not use the same name together. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- The fenced token survives expiry, release, a restart, and a failover: a later owner always gets a larger one. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- `LEASE` returns a new fencing token, or a null bulk when the lease is still held. After expiry, `RELEASE`, `DEL`, a restart, a failover, or a slot move, the next grant is larger (generation in the top bits, then a counter with a clock floor). The key's TTL is the lease deadline. `GET` of a lease is `WRONGTYPE`. https://ruviodb.com/llms/commands.txt
- `RENEW key token ttl_ms` returns 1, or 0 when the token is expired, released, or not the holder. It never creates a holder and never changes the token sequence. https://ruviodb.com/llms/commands.txt
- `RELEASE` returns 1 or 0. A mismatch leaves the holder. A released lease removes its key. The next `LEASE` gets a larger token. https://ruviodb.com/llms/commands.txt
- `TryAcquireAsync` returns null when the lock stayed taken. `AcquireAsync` throws `TimeoutException`. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- `AutoRenew` defaults to on and renews every third of the lease. If a whole lease passes without a successful renewal, or another owner holds it, `IsHeld` becomes false and `LockLost` is cancelled. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- Locks are not reentrant. Lease times are server TTLs. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- If a failover loses the key, two owners can overlap for up to a lease. The page says work that must not overlap should also check a fencing token. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- `AddRuvioSingleton` runs the worker on the one instance that holds the lock. Default retry is 5 seconds. Default lease is 30 seconds. A crash holds the work for at most one lease. It needs `AutoRenew`. With `Fenced` on, `leadership.FencingToken` is set. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- `[RuvioLock]` waits `WaitMilliseconds` (default 0), then answers `RejectStatusCode` (default 409). The lease renews while the endpoint runs and is released when the response finishes. If the lock cannot be asked for, the middleware answers 503. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
- The same client helper: `TryAcquireLeaseAsync` returns `RuvioLease` whose `Token` is the fencing token. https://ruviodb.com/llms/clients/csharp.txt

```csharp
services.AddRuvioDistributedLock(options => options.KeyPrefix = "myapp:lock:");

await using IRuvioLock? held = await locks.TryAcquireAsync(
    "nightly-report", lease: TimeSpan.FromSeconds(30), wait: TimeSpan.FromSeconds(5));
if (held is null) return;
await RunReportAsync(held.LockLost);
```

https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt

```csharp
services.AddRuvioDistributedLock(options => options.Fenced = true);

await using IRuvioLock held = await locks.AcquireAsync(
    "ledger", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));
await SaveAsync(held.FencingToken!.Value, held.LockLost);
```

https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt

```csharp
await using RuvioLease? lease = await db.TryAcquireLeaseAsync("lease:job", TimeSpan.FromSeconds(30));
long fencingToken = lease!.Token;
```

https://ruviodb.com/llms/clients/csharp.txt

- `RunOncePerPeriodAsync` is one `ONCE` command, not a lease. The marker is set before the work runs, so a failed run is not repeated in that period. https://ruviodb.com/llms/integrations/dotnet/distributed-lock.txt
