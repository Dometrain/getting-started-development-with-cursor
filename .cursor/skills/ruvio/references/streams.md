# Streams

Consumer-group commands for a vote queue that any worker replica can read. Facts below are limits recorded for those commands.

- `XADD` auto id is `*` or `ms-*`. Explicit ids are `ms-seq`. A millisecond behind the top id is rejected. https://ruviodb.com/llms/commands.txt
- `MAXLEN` and `MINID`, including `~` and `=`, both trim exactly. They cannot be combined. The approximate trim option `LIMIT` is not accepted. https://ruviodb.com/llms/commands.txt
- `NOMKSTREAM` returns a null bulk and does not create a missing key. https://ruviodb.com/llms/commands.txt
- `EX` or `PX` on `XADD` sets a deadline only when the call creates the key. https://ruviodb.com/llms/commands.txt
- `DELAY` hides the new entry from `XREADGROUP` until that many milliseconds pass. The hold is a WAL record and a snapshot trailer, written only when a delay is set. https://ruviodb.com/llms/commands.txt
- `XLEN` returns 0 for a missing key. https://ruviodb.com/llms/commands.txt
- `XGROUP CREATE` creates one group at `$`, `0`, or an explicit id. `MKSTREAM` creates an empty stream when the key is missing. https://ruviodb.com/llms/commands.txt
- `XREADGROUP` reads one stream and the `>` id only. `COUNT` limits the new entries. `BLOCK` waits on this shard until a later `XADD` and delivers that id to the oldest waiting consumer of the group. `BLOCK 0` waits until an entry or shutdown. https://ruviodb.com/llms/commands.txt
- `NOACK` advances the group id and does not add a pending entry. https://ruviodb.com/llms/commands.txt
- `XACK` removes the id from that group's pending list and leaves the stream entry stored. https://ruviodb.com/llms/commands.txt
- Because `XACK` leaves the entry stored, `XLEN` still counts acknowledged entries. The pending count is the `XPENDING` summary's count. https://ruviodb.com/llms/commands.txt
- `XPENDING` with no arguments after the group returns count, smallest id, greatest id, and consumers sorted by name. https://ruviodb.com/llms/commands.txt
- The extended `XPENDING` form is optional `IDLE`, an inclusive start, an inclusive end, a count, and an optional consumer. Each row is id, consumer, idle milliseconds, and delivery count. https://ruviodb.com/llms/commands.txt
- `XCLAIM` claims pending ids idle for at least min-idle milliseconds. `IDLE` and `TIME` cannot be combined. `FORCE` claims an id that is in the stream but not pending and does not move the group cursor. https://ruviodb.com/llms/commands.txt
- `XAUTOCLAIM` scans pending ids after the start id and claims those idle for at least min-idle milliseconds. The reply is the next start id, the claimed entries, and ids dropped because the stream entry is gone. https://ruviodb.com/llms/commands.txt
- `XINFO GROUPS` reports name, consumers, pending, last-delivered-id, and lag. Consumers are the distinct names on the pending list. https://ruviodb.com/llms/commands.txt
- `XREAD` does not read a consumer group. https://ruviodb.com/llms/commands.txt
- A blocking `XREADGROUP BLOCK` holds the connection until it returns. Use a separate client for it. https://ruviodb.com/llms/clients/csharp.txt
- Client `XREAD` / `XREADGROUP` options include `COUNT`, `BLOCK`, and `NOACK` (group reads only). `XGROUP CREATE` with `createStream: true` sends `MKSTREAM`. https://ruviodb.com/llms/clients/csharp.txt

```csharp
await db.AddStreamEntryAsync(
    "events",
    StreamIds.New,
    StreamTrim.MaxLen(10_000, StreamTrimMode.Approximate),
    ("type", "login"));
```

https://ruviodb.com/llms/clients/csharp.txt

- `StreamTrim.MaxLen(..., StreamTrimMode.Approximate)` is the client example. The server still trims `~` and `=` exactly, and it does not accept `LIMIT`. https://ruviodb.com/llms/commands.txt
