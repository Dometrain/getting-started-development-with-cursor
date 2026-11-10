Date: 2026-11-05
Model: Auto

## Before the Ruvio skill

1. Ruvio is a database server with a command table, WAL and snapshot persistence, and clients for several languages including .NET. I do not know which wire protocol it speaks. [low]
2. A .NET client is shipped to NuGet in the release workflow. I do not know the package id or how a connection is opened. [low]
3. I do not know a Ruvio command that limits a user to 10 requests per second. [low]
4. I do not know a Ruvio lock command that issues a fencing token so only one worker runs a job. [low]
5. I do not know a Ruvio command that guarantees the same request is processed only once. [low]
6. I do not know Ruvio consumer-group commands, or how a live consumer takes over messages from a crashed one. [low]
7. I do not know whether a browser can connect to Ruvio directly for live updates. [low]

## After the Ruvio skill

1. Ruvio is the server the shared .NET client connects to (default 127.0.0.1:6379), and that client speaks RESP2. [high] (dotnet-client.md)
2. The NuGet package is Ruvio.Client. Open it once with RuvioConnection.ConnectAsync (host and port), then GetDefaultDatabaseAsync, and reuse that connection; a password is sent as AUTH only when set. [high] (dotnet-client.md)
3. Call LIMIT on that user's key with a max of 10 and a one-second fixed window (the default). A full window returns a null bulk and does not count the rejection. [high] (rate-limiting.md)
4. Acquire the job with LEASE (TryAcquireLeaseAsync); renew with RENEW and free it with RELEASE, and let only the holder run the work. The fencing token only grows, and the work should check it, because a failover can leave two holders for up to one lease. [high] (lease-and-locks.md)
5. Use IDEM BEGIN with the request fingerprint and an owner, run the work only when the reply is acquired, then COMPLETE with the result. A later BEGIN with the same fingerprint returns completed and the saved result; a crash after the side effect and before COMPLETE is not exactly-once. [high] (idempotency.md)
6. Append with XADD, create the group with XGROUP CREATE, read new entries with XREADGROUP using the > id, then XACK. Take over a crashed consumer with XCLAIM or XAUTOCLAIM on pending ids idle for at least the minimum idle time. [high] (streams.md)
7. No. Live updates are PUBLISH and SUBSCRIBE on the RESP2 client, which opens a second TCP socket for the subscription. [high] (pubsub.md)

## Comparison

| Question | Before (short) | After (short) | Correct? (Before / After) |
|---|---|---|---|
| 1. What is Ruvio, and which protocol? | Database server; protocol unknown | Shared client server; speaks RESP2 | No / Yes |
| 2. .NET package and connection | Package and connect unknown | Ruvio.Client via ConnectAsync | No / Yes |
| 3. 10 requests per second | No command known | LIMIT, max 10 per second | No / Yes |
| 4. One worker, fencing token | No lock command known | LEASE, RENEW, RELEASE | No / Yes |
| 5. Process the same request once | No command known | IDEM BEGIN then COMPLETE | No / Yes |
| 6. Queue and crashed consumer | No consumer-group commands | XREADGROUP, XACK, XAUTOCLAIM | No / Yes |
| 7. Browser live updates | Unknown | No direct browser connection | No / Yes |
