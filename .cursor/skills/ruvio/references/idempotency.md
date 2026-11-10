# Idempotency

- One vote per poll uses `EXISTS` or `ONCE`. `EXISTS` requires same-slot keys in multi-shard mode. https://ruviodb.com/llms/commands.txt
- `ONCE` — the first call returns 1 and keeps the key until the TTL. A later call returns 0 and does not refresh the TTL. After the deadline the next call is first again. The marker exists only on keys written with `ONCE`. The first call is a WAL record. `GET` of an once key is `WRONGTYPE`. https://ruviodb.com/llms/commands.txt
- `SET` supports `NX`. https://ruviodb.com/llms/commands.txt
- If a call still needs `IDEM`, use the contract below, https://ruviodb.com/llms/idem.txt, or the .NET packages https://ruviodb.com/llms/integrations/dotnet/idempotency.txt and https://ruviodb.com/llms/integrations/dotnet/idempotent-executor.txt.

- `IDEM` is a native command on the shard that owns the key. It is not a Lua script and not HTTP-only. `ONCE` is a separate lighter TTL marker. https://ruviodb.com/llms/idem.txt
- Wire form: `IDEM key BEGIN fingerprint owner ttl_ms`, `IDEM key COMPLETE fingerprint owner result`, `IDEM key ABORT fingerprint owner`. Actions are case-insensitive. https://ruviodb.com/llms/idem.txt
- Fingerprint and owner are opaque binary-safe values of 1–128 bytes. The server compares them. It does not compute a fingerprint or authenticate an owner. https://ruviodb.com/llms/idem.txt
- `BEGIN` retention is an integer from 1 to 604800000 milliseconds (7 days). https://ruviodb.com/llms/idem.txt
- `BEGIN` replies: `acquired` (this attempt owns a new pending record), `pending` (same fingerprint, not completed; do not run it again), `conflict` (different fingerprint), `completed` plus the saved result bytes. https://ruviodb.com/llms/idem.txt
- Only `acquired` writes. A longer TTL on retry does not extend the original deadline. https://ruviodb.com/llms/idem.txt
- `COMPLETE` returns integer 1 for a live pending record with the exact owner and fingerprint, and for an identical repeat of the same completed result. It returns 0 for a missing or expired record, an owner or fingerprint mismatch, or a different result. Completion does not extend retention. https://ruviodb.com/llms/idem.txt
- `ABORT` returns 1 when it removes a live pending record with the exact owner and fingerprint. It returns 0 for a completed record. A stored result is never removed. Abort only when the operation did not take effect, or is safe to run again. https://ruviodb.com/llms/idem.txt
- The result is binary-safe and limited to 4 MiB. https://ruviodb.com/llms/idem.txt
- `TYPE` reports `string`. `GET` exposes the packed record, not just the result. `SET`, `DEL`, `PERSIST`, expiry edits, and other ordinary writes can destroy the guarantee. https://ruviodb.com/llms/idem.txt
- This is not exactly-once execution in an external database. A process may do the side effect and crash before `COMPLETE`. A lost `BEGIN` reply must not cause the business operation to run again. https://ruviodb.com/llms/idem.txt
- Creation and completion store the value and the absolute deadline together. Recovery keeps owner, state, result, and deadline. https://ruviodb.com/llms/idem.txt
- The commands page repeats: do not modify an `IDEM` key with string or expiry commands, and it is not exactly-once for external effects. https://ruviodb.com/llms/commands.txt
- C#: `TryBeginIdempotencyAsync` returns `IdempotencyBegin(State, Result)`. `CompleteIdempotencyAsync` takes a text result and returns a boolean. `AbortIdempotencyAsync` returns false for a completed record or another owner. `ExecuteBinaryAsync` sends arbitrary token and result bytes. https://ruviodb.com/llms/clients/csharp.txt
- `IRuvioClient.BeginIdempotencyAsync` returns the raw RESP state. `CompleteIdempotencyAsync` is the boolean acknowledgment. https://ruviodb.com/llms/idem.txt
- `Ruvio.AspNetCore.Idempotency` calls these native methods and owns HTTP fingerprinting, bounded capture, and response serialization. The server stays HTTP-agnostic. https://ruviodb.com/llms/idem.txt
- Only `acquired` authorizes the caller to execute. No ambiguous write is automatically retried. https://ruviodb.com/llms/clients/csharp.txt

```text
IDEM order:42 BEGIN request-digest attempt-token 60000
# ["acquired"]
IDEM order:42 COMPLETE request-digest attempt-token saved-result
# 1
IDEM order:42 BEGIN request-digest another-attempt 60000
# ["completed", "saved-result"]
```

https://ruviodb.com/llms/idem.txt

```csharp
IdempotencyBegin begin = await db.TryBeginIdempotencyAsync(
    "idem:pay:1", fingerprint, owner, TimeSpan.FromMinutes(10));
if (begin.State == IdempotencyState.Acquired)
    await db.CompleteIdempotencyAsync("idem:pay:1", fingerprint, owner, "ok");
```

https://ruviodb.com/llms/clients/csharp.txt

