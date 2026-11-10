---
name: ruvio
description: "Supplies LivePoll's verified Ruvio facts for the .NET client, Docker, streams, pub/sub, leases, rate limits, and idempotency. Use when a task involves Ruvio, including shared counters, sessions, the vote queue, live snapshots, locks, rate limits, one-time acceptance, business metrics, or the Ruvio container."
---

# Ruvio

## Read the matching reference first

Pick the row in the table, then open that file. Command limits for every row are indexed in [commands-index.md](references/commands-index.md). Do not fill a gap from memory.

## What LivePoll keeps in Ruvio

Use only the opening list in [commands-index.md](references/commands-index.md).

## Client, connection, and Docker

- Client, packages, and connection: [dotnet-client.md](references/dotnet-client.md)
- Image, port, password, durability, and data directory: [docker.md](references/docker.md)

## Which command for which problem

| Problem | Ruvio feature | Reference file |
|---|---|---|
| Session or other string | SET, GET | [commands-index.md](references/commands-index.md) |
| Counter | INCR, INCRBY | [commands-index.md](references/commands-index.md) |
| One acceptance per key | EXISTS, ONCE | [idempotency.md](references/idempotency.md) |
| Same request with a saved result | IDEM | [idempotency.md](references/idempotency.md) |
| Lock without a fencing token | SET NX, SET IFEQ, DELEX | [lease-and-locks.md](references/lease-and-locks.md) |
| One holder with a fencing token | LEASE, RENEW, RELEASE | [lease-and-locks.md](references/lease-and-locks.md) |
| Shared rate limit | LIMIT | [rate-limiting.md](references/rate-limiting.md) |
| Vote queue any replica can read | XADD, XGROUP CREATE, XREADGROUP, XACK | [streams.md](references/streams.md) |
| Crashed consumer's pending entries | XCLAIM, XAUTOCLAIM | [streams.md](references/streams.md) |
| Pending count versus entries still stored | XPENDING, XLEN | [streams.md](references/streams.md) |
| Live snapshot to subscribers | PUBLISH, SUBSCRIBE | [pubsub.md](references/pubsub.md) |

## Do not

1. Do not answer from memory when a file under [references/](references/) records the fact.
2. Do not invent the wire protocol. See [dotnet-client.md](references/dotnet-client.md).
3. Do not guess the package or how a connection opens. See [dotnet-client.md](references/dotnet-client.md).
4. Do not invent a shared rate limit. See [rate-limiting.md](references/rate-limiting.md).
5. Do not invent a single-worker lock. See [lease-and-locks.md](references/lease-and-locks.md).
6. Do not invent once-only acceptance, or treat it as exactly-once for an external database. See [idempotency.md](references/idempotency.md).
7. Do not read the vote queue without a consumer group, or drop a crashed consumer's pending entries. See [streams.md](references/streams.md).
8. Do not treat a live snapshot as stored or written to the WAL. See [pubsub.md](references/pubsub.md) and [docker.md](references/docker.md).
