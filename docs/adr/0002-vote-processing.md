# ADR-0002: Vote acceptance and processing

Status: Accepted
Date: 2026-11-06

## Context and Requests <!-- HUMAN -->

A user votes on a poll. Up to 1,000 simulated users vote at the same time.
- R1: A user can vote only once per poll, even if the same request
      arrives twice or two requests arrive at the same moment.
- R2: No accepted vote is lost, even if a Worker crashes in the middle of its work.
- R3: The vote endpoint answers within 100 ms (p95) with 1,000 voters.
- R4: Every accepted vote reaches Postgres within 5 seconds.
- R5: A vote on a poll that has ended is rejected.
- R6: A vote that keeps failing does not block other votes. It is kept aside
      so we can look at it later.
- R7: We use Ruvio for this. We do not add RabbitMQ or another broker.
- R8: An accepted vote survives a Ruvio restart. (Ruvio's default durability
      is memory only; choose a durability mode and say what it costs.)

## Out of Scope <!-- HUMAN -->

- Changing a vote
- A screen for managing failed votes
- Replaying votes from history
- Any message broker other than Ruvio

## Options and Decision <!-- AI PROPOSES -->

The accept path is the same in every option. The API records the vote in Ruvio and answers. A Worker reads a Ruvio stream and stores the vote once in Postgres under the username. The options are the `RUVIO_DURABILITY` profiles in the Ruvio docker notes. `snapshot` is one of the four profiles; this project does not use it.

| Option | Durability | Cost |
| --- | --- | --- |
| 1 | `memory` (file default) | Keeps nothing on disk. A Ruvio restart drops every accepted vote that is not yet in Postgres. |
| 2 | `everysec` | Acknowledges after the bytes reach the kernel, and runs `fdatasync` every `sync_interval_ms` (default 1000). That interval is the loss window. |
| 3 | `always` | `fdatasync`s the client batch before the replies are sent. The sync time is not stated. The wait would sit on the accept path whose p95 limit is 100 ms. This record keeps `everysec`. |

**Decision:** Option 2. Set `RUVIO_DURABILITY=everysec`. The API accepts into a Ruvio stream. A Worker stores each accepted vote once in Postgres.

1. R1. Call `ONCE` on one key per poll and username, with a TTL of 24 hours. That is well past the 10-minute open window. The first call returns 1 and is a WAL record. A later call returns 0 and does not refresh the TTL. `XADD` runs only when `ONCE` returns 1. Postgres keeps one row per poll and username, so a second delivery does not add a second row. The idempotency notes name `EXISTS` or `ONCE` for one vote per poll. `ONCE` is the call that records the first time. `IDEM` is a different command, its retention stops at 7 days, and it is not exactly-once for a write in another database. Two `ONCE` calls at the same instant are proved by `OneVotePerPoll_DuplicateAndSameMoment`, which must produce one acceptance.
2. R2. Worker replicas share one consumer group. `XREADGROUP` does not use `NOACK`, so the entry stays pending. `XACK` runs only after the Postgres commit. A replica that dies first leaves the entry pending. Another replica takes it with `XAUTOCLAIM` once it has been idle for at least 1000 ms, then stores it. A blocking `XREADGROUP` holds its connection, so that read uses its own client.
3. R3. The vote endpoint answers after `XADD`. It does not insert the vote, it does not wait for a Worker, and it does not read or write Postgres. It reads the poll end time from Ruvio, then that API process keeps the value in memory. A later vote on that process uses the cache. Another replica reads Ruvio on its first vote for that poll. The calls on the path are the end-time read, `ONCE`, `XADD`, and the metric counters. `everysec` acknowledges when the bytes are in the kernel, before `fdatasync`. The 1,000-voter load test in ADR-0005 proves p95 under 100 ms. If that test fails, add API replicas. The accept path stays this one.
4. R4. The worker waits on `XREADGROUP BLOCK`. The vote `XADD` sets no `DELAY`, so the entry is visible to the group at once. The 1000 ms claim wait is inside the 5 second limit. A vote that the worker has set aside under R6 is the exception to this limit.
5. R5. A poll is open for 10 minutes. Poll creation writes that end time to Ruvio with no expiry, so the key outlives the window and an `everysec` restart. Immediately before `ONCE`, the API reads that end time (from memory after the first read) and, once it has passed, rejects the vote with no `ONCE` and no `XADD`. A missing poll is rejected from that Ruvio key, not from Postgres, on this path. A vote already accepted is still stored when the window ends before the insert.
6. R6. The extended `XPENDING` row carries the delivery count. When the count is greater than 3, the worker appends that entry to a side stream, then `XACK`s it on the vote stream. Later votes still move on `>`. The notes list no dead-letter command. This is `XADD`, `XPENDING`, and `XACK` only. Those entries stay on the side stream for later inspection. There is no management screen.
7. R7. The queue, the one-vote marker, and the pending list are Ruvio commands: `ONCE`, `XADD`, `XGROUP`, `XREADGROUP`, `XACK`, `XAUTOCLAIM`, and `XPENDING`.
8. R8. `everysec` writes a log under the data directory. The directory stays the default `./data` (`RUVIO_DATA_DIR`). The price of that mode is the loss window in Consequences.

The vote stream is not trimmed. `MAXLEN` and `MINID` both trim exactly, and a trim can drop a vote that is not in Postgres yet. `XACK` leaves the entry stored, so `XLEN` still counts acknowledged entries and is not the worker backlog. Backlog is the group's pending count plus its lag (`XPENDING` and `XINFO GROUPS`). The notes name lag and do not define it further.

## Consequences and Risks <!-- AI PROPOSES -->

1. Durability cost for R8. `everysec` acknowledges after the bytes are written to the kernel and runs `fdatasync` in the background every `sync_interval_ms` (default 1000). That interval is the loss window. A crash inside it can drop a vote the API has already accepted. A restart is not required to `fdatasync` before it exits. This record keeps `everysec` because an `always` sync would sit on the 100 ms path and its duration is not stated. `memory` keeps nothing on disk. `snapshot` is unused. Durability cannot change under a live process. A WAL-backed profile takes an exclusive lock on `<data_dir>/LOCK`.
2. The log lives under `./data`. Compose keeps that directory so a restart still has it. `.cursor/skills/ruvio/SKILL.md` supplies the docker, streams, idempotency, and commands-index notes used here.
3. `PUBLISH` is not written to the WAL. `ONCE`'s first call is a WAL record. A `DELAY` hold is a WAL record only when a delay is set. This record requires the accepted vote stream entry to survive an `everysec` restart after the sync (Q5), and `RuvioRestart_AfterSync_VoteStillStored` proves it. `GET` of an `ONCE` key is `WRONGTYPE`, so the vote body cannot be stored in the marker.
4. `ONCE` lasts 24 hours. After that deadline the next call is first again. The 24 hours outlast the 10-minute window. After the poll has ended, R5 still rejects the vote from the end-time key. Two calls at the same instant are the R1 test, which must accept only one.
5. If `ONCE` returns 1 and `XADD` then fails, the marker stays. That username can be blocked with no queued vote. The API answers accepted only after both calls have succeeded. That is the rule. The notes do not offer a delete for the marker, so this path does not try one.
6. The vote stream keeps every acknowledged entry, because `XACK` does not remove it and this decision does not trim. The data directory grows by every accepted vote. The same is true of the side stream, which is how failed votes stay available to inspect.
7. There is no documented dead-letter command. A crash after the side-stream `XADD` and before `XACK` leaves the vote pending, so a worker tries it again. A vote kept aside does not reach Postgres. R4 covers a vote the worker stores.
8. `IDEM` recovery keeps its own record. It does not make the Postgres insert exactly-once. The unique poll-and-username row is what makes a redelivery store one vote. A worker that finds the row already present `XACK`s the entry.
9. The notes give no command latency. R3 is proved by the load test. The vote path does not read Postgres. `PUBLISH` is not part of this decision and is not stored by any durability profile.

## Resolved questions

- Q4. A vote accepted as the 10-minute window closes is still stored when the Worker writes it after the poll has closed. Recorded in R5.
- Q7. The vote path touches only Ruvio (end-time read, `ONCE`, `XADD`, counters). Each API keeps that end time in memory after the first read. The ADR-0005 load test proves p95 under 100 ms. A failure means more API replicas, not a different accept path.
- The `ONCE` TTL is 24 hours.
- Command latency is not a separate product setting. `VoteEndpoint_P95_Under100ms_With1000Voters` is the proof.

## Verification <!-- AI PROPOSES, HUMAN CHECKS -->

| Request | Test name | What the test proves |
| --- | --- | --- |
| R1 | OneVotePerPoll_DuplicateAndSameMoment | The same username voting twice, including two overlapping requests, produces one acceptance and one stored row. |
| R2 | WorkerCrash_BeforeAck_VoteStoredOnce | Killing the worker after acceptance and before `XACK` still leaves that vote stored once under the username. |
| R3 | VoteEndpoint_P95_Under100ms_With1000Voters | With 1,000 concurrent voters, the vote endpoint's p95 is under 100 ms. A miss is met by adding API replicas, and the accept path stays Ruvio-only. |
| R3 | VotePath_TouchesOnlyRuvio | Accepting a vote reads the end time, calls `ONCE` and `XADD`, and updates counters. It does not open Postgres. |
| R3 | EndTimeCachedAfterFirstRead | The second vote for a poll on the same API process does not read the end-time key again. |
| R1 | OnceMarker_TtlIs24Hours | The one-vote marker is kept for 24 hours, and a second vote inside that time is rejected. |
| R4 | AcceptedVote_ReachesPostgres_Within5Seconds | A vote the worker can store is in Postgres within 5 seconds of the API accepting it. |
| R5 | EndedPoll_VoteRejected | A vote after the poll's 10 minutes is rejected and is neither queued nor stored. |
| R6 | FailingVote_KeptAside_OtherVotesProceed | A vote whose delivery count is greater than 3 is on the side stream, and a later good vote is still stored. |
| R7 | VoteQueue_IsRuvioStream | Acceptance, the one-vote marker, and worker delivery all go through Ruvio, with no other broker in the path. |
| R8 | RuvioRestart_AfterSync_VoteStillStored | With `./data` still present, a vote left past the default 1000 ms `everysec` sync is still stored once after Ruvio restarts. |
