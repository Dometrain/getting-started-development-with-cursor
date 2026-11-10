# Commands this project may use

Sessions, counters, the vote queue, live snapshots, leases, rate limits, and idempotent acceptance. One line each. Syntax limits are from the commands page.

- `AUTH` — default user or a configured ACL user; `AUTH username password` is supported. https://ruviodb.com/llms/commands.txt
- `PING` — listed with no extra limit; after `SUBSCRIBE` the connection still accepts `PING`. https://ruviodb.com/llms/commands.txt
- `SELECT` — indexes `0` through `databases - 1` (16 by default, at most 256); cluster mode allows only `0`. https://ruviodb.com/llms/commands.txt
- `GET` — `WRONGTYPE` on a non-string key. https://ruviodb.com/llms/commands.txt
- `SET` — supports `EX`, `PX`, `EXAT`, `PXAT`, `KEEPTTL`, `NX`, `XX`, and `GET`; `SET key value IFEQ expected` writes only when the string equals `expected`. https://ruviodb.com/llms/commands.txt
- `DEL` — same-slot keys in multi-shard mode. https://ruviodb.com/llms/commands.txt
- `EXISTS` — same-slot keys in multi-shard mode. https://ruviodb.com/llms/commands.txt
- `ONCE` — first call returns 1 and keeps the key until the TTL; a later call returns 0 and does not refresh the TTL; `GET` of an once key is `WRONGTYPE`. https://ruviodb.com/llms/commands.txt
- `EXPIRE` — a relative expire of zero or less deletes the key; accepts one of `NX`, `XX`, `GT`, `LT`. https://ruviodb.com/llms/commands.txt
- `INCR` — signed 64-bit decimal; `EX` or `PX` sets a deadline only when the call creates the key. https://ruviodb.com/llms/commands.txt
- `DECR` — signed 64-bit decimal; `EX` or `PX` sets a deadline only when the call creates the key. https://ruviodb.com/llms/commands.txt
- `INCRBY` — signed 64-bit; `INCRBY key n MAX limit` returns a null bulk and writes nothing when the result would pass `limit`; option order is `[EX|PX] [MAX]`. https://ruviodb.com/llms/commands.txt
- `DECRBY` — signed 64-bit decimal; `EX` or `PX` sets a deadline only when the call creates the key. https://ruviodb.com/llms/commands.txt
- `DELEX` — `DELEX key IFEQ value` deletes the string only when it equals `value` and returns 1, otherwise 0. https://ruviodb.com/llms/commands.txt
- `IDEM` — `BEGIN` reserves or returns pending, conflict, or completed; `COMPLETE` publishes for the matching live owner; `ABORT` removes a matching pending record only; tokens are 1–128 bytes, retention is 1 ms–7 days, result is at most 4 MiB; not exactly-once for external effects. https://ruviodb.com/llms/commands.txt
- `LEASE` — returns a new fencing token, or a null bulk when the lease is still held; later grants stay larger after expiry, `RELEASE`, `DEL`, a restart, a failover, or a slot move. https://ruviodb.com/llms/commands.txt
- `RENEW` — `RENEW key token ttl_ms` moves a live `LEASE` or `SEMAPHORE` deadline to now plus `ttl_ms`; returns 1 or 0; never creates a holder and never changes the token. https://ruviodb.com/llms/commands.txt
- `RELEASE` — releases a lease when the token matches the current holder and returns 1 or 0; a mismatch leaves the holder; the next `LEASE` gets a larger token. https://ruviodb.com/llms/commands.txt
- `LIMIT` — fixed window by default; returns the remaining allowance, or a null bulk when full, and does not count a rejection; `SLIDING` and `TOKENS` exist; a key keeps the mode it was created with; `GET` of a limit is `WRONGTYPE`. https://ruviodb.com/llms/commands.txt
- `SEMAPHORE` — `SEMAPHORE key max ttl_ms` returns a token while fewer than `max` holders are live, or a null bulk when full. https://ruviodb.com/llms/commands.txt
- `XADD` — auto id is `*` or `ms-*`; `MAXLEN` and `MINID` both trim exactly and cannot be combined; `NOMKSTREAM` does not create a missing key; `DELAY` hides the entry from `XREADGROUP` until that many milliseconds pass. https://ruviodb.com/llms/commands.txt
- `XLEN` — returns 0 for a missing key. https://ruviodb.com/llms/commands.txt
- `XGROUP CREATE` — creates one group at `$`, `0`, or an explicit id; `MKSTREAM` creates an empty stream when the key is missing. https://ruviodb.com/llms/commands.txt
- `XREADGROUP` — one stream and the `>` id only; `COUNT` limits new entries; `BLOCK` waits on this shard; `NOACK` advances the group id and does not add a pending entry. https://ruviodb.com/llms/commands.txt
- `XACK` — removes the id from that group's pending list and leaves the stream entry stored. https://ruviodb.com/llms/commands.txt
- `XPENDING` — no arguments after the group returns count, smallest id, greatest id, and consumers; the extended form takes optional `IDLE`, an inclusive range, a count, and an optional consumer. https://ruviodb.com/llms/commands.txt
- `XCLAIM` — claims pending ids idle at least min-idle milliseconds; accepts `IDLE`, `TIME`, `RETRYCOUNT`, `FORCE`, `JUSTID`, and `LASTID`. https://ruviodb.com/llms/commands.txt
- `XAUTOCLAIM` — claims pending ids idle at least min-idle milliseconds, up to `COUNT`; the reply is the next start id, the claimed entries, and ids dropped because the entry is gone. https://ruviodb.com/llms/commands.txt
- `XINFO GROUPS` — reports name, consumers, pending, last-delivered-id, and lag. https://ruviodb.com/llms/commands.txt
- `PUBLISH` — delivers on this shard to exact `SUBSCRIBE` clients and matching `PSUBSCRIBE` patterns; not stored and not written to the WAL; the commands page says other shards do not receive it. https://ruviodb.com/llms/commands.txt
- `SUBSCRIBE` — exact channels on this shard; after a subscription the connection accepts `SUBSCRIBE`, `UNSUBSCRIBE`, `PSUBSCRIBE`, `PUNSUBSCRIBE`, `SSUBSCRIBE`, `SUNSUBSCRIBE`, and `PING`. https://ruviodb.com/llms/commands.txt
- `UNSUBSCRIBE` — no arguments drops every exact classic channel on this connection and leaves patterns in place. https://ruviodb.com/llms/commands.txt
