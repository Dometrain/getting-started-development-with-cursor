# .NET client

## Packages

- Package `Ruvio.Client` on NuGet. Install: `dotnet add package Ruvio.Client`. https://ruviodb.com/llms/clients/csharp.txt
- Package `Ruvio.Client.AspNetCore` registers that client in an ASP.NET Core service collection. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- The client speaks RESP2 and does not send `CLIENT SETINFO` or `HELLO`. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- Typed methods cover every command in the compatibility matrix (299/299). `ExecuteAsync` sends the same argument lists as `redis-cli`. https://ruviodb.com/llms/clients/csharp.txt
- Integration packages do not own or dispose the client singleton. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt

## Connection

- The application chooses the server address and, if it uses one, the database number. It opens the client once and shares it. It does not choose shards and does not send `SELECT` between commands. https://ruviodb.com/llms/connection-model.txt
- On connect the client sends `CLUSTER SLOTS`. One shard stays one socket. A sharded server gets one socket per shard, and each key goes to `hash_slot(key)`. https://ruviodb.com/llms/clients/csharp.txt
- One handle is one database. `GetDatabaseAsync(n)` returns one cached client per database. Cluster mode allows only database 0. https://ruviodb.com/llms/connection-model.txt
- A password is sent as `AUTH` only when `RuvioClientOptions.Password` is set. A reconnect authenticates with the options again. https://ruviodb.com/llms/clients/csharp.txt
- Command sockets multiplex calls. Reuse the connection. `MaxInFlightRequests` defaults to 256. https://ruviodb.com/llms/clients/csharp.txt
- `Reconnect` defaults to true. The command that finds a dropped socket fails and is not sent again. https://ruviodb.com/llms/clients/csharp.txt
- A blocking `XREADGROUP BLOCK` holds that connection until it returns. Use a separate client for it. https://ruviodb.com/llms/clients/csharp.txt
- Subscribe opens a second TCP socket. `GET`, `SET`, and `PUBLISH` stay on the command socket. https://ruviodb.com/llms/clients/csharp.txt
- Default server address in the setup page is `127.0.0.1:6379`. https://ruviodb.com/llms/setup.txt

```csharp
await using var connection = await RuvioConnection.ConnectAsync("127.0.0.1", 6379);
var db = await connection.GetDefaultDatabaseAsync();
await db.SetAsync("session:ada", "hello");
string? value = await db.GetStringAsync("session:ada");
```

https://ruviodb.com/llms/clients/csharp.txt

```csharp
await using var securedConnection = await RuvioConnection.ConnectAsync(new RuvioClientOptions
{
    Host = "10.0.0.5",
    Port = 6379,
    Username = "app",
    Password = "secret",
    Database = 4,
    ConnectTimeout = TimeSpan.FromSeconds(2),
});
```

https://ruviodb.com/llms/clients/csharp.txt

## DI registration

- `AddRuvioClient` registers the connection owner and the database-scoped command client as separate singletons under one DI lifetime. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- Inject `IRuvioClient` to send commands. Inject `IRuvioConnection` to create database clients, reconnect, or handle lifecycle events. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- Disposing the DI container disposes the connection and every database client it created. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- `Reconnect` defaults to true. The command that discovers the drop fails and is never sent again. Pub/Sub channels are subscribed again. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt
- The optional health check sends one bounded `PING` through the existing `IRuvioClient`. Default timeout is five seconds. It does not check persistence or write availability. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt

```csharp
builder.Services.AddRuvioClient("127.0.0.1", 6379, database: 2);
builder.Services.AddRuvioClient(builder.Configuration.GetSection("Ruvio"));
builder.Services.AddHealthChecks().AddRuvio(tags: ["ready"]);
```

https://ruviodb.com/llms/clients/csharp-aspnetcore.txt

```json
{
  "Ruvio": {
    "Host": "127.0.0.1",
    "Port": 6379,
    "Database": 2,
    "Reconnect": true
  }
}
```

https://ruviodb.com/llms/clients/csharp-aspnetcore.txt

The `database: 2` value is the page's example, not a LivePoll setting. https://ruviodb.com/llms/clients/csharp-aspnetcore.txt

## Typical calls

```csharp
long visits = await db.IncrementAsync("visits");
```

https://ruviodb.com/llms/clients/csharp.txt

```csharp
await db.PublishAsync("news", "hello");
await db.SubscribeAsync("news", msg => Console.WriteLine(msg.Payload));
```

https://ruviodb.com/llms/clients/csharp.txt

```csharp
await using RuvioLease? lease = await db.TryAcquireLeaseAsync("lease:job", TimeSpan.FromSeconds(30));
long fencingToken = lease!.Token;
```

https://ruviodb.com/llms/clients/csharp.txt

```csharp
RateLimitResult decision = await db.RateLimitAsync("rl:ada", max: 100, TimeSpan.FromMinutes(1));
```

https://ruviodb.com/llms/clients/csharp.txt

```csharp
IdempotencyBegin begin = await db.TryBeginIdempotencyAsync("idem:pay:1", fingerprint, owner, TimeSpan.FromMinutes(10));
if (begin.State == IdempotencyState.Acquired)
    await db.CompleteIdempotencyAsync("idem:pay:1", fingerprint, owner, "ok");
```

https://ruviodb.com/llms/clients/csharp.txt

```csharp
await db.AddStreamEntryAsync("events", StreamIds.New, StreamTrim.MaxLen(10_000, StreamTrimMode.Approximate), ("type", "login"));
```

https://ruviodb.com/llms/clients/csharp.txt

- `XREAD` / `XREADGROUP` options on the client: `COUNT`, `BLOCK`, and `NOACK` (group reads only). https://ruviodb.com/llms/clients/csharp.txt
- `TryBeginIdempotencyAsync` returns `IdempotencyBegin(State, Result)`. Only `acquired` authorizes the caller to run the operation. https://ruviodb.com/llms/clients/csharp.txt
