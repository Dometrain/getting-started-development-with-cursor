# ADR-0001: Architecture and data storage

Status: Accepted
Date: 2026-11-06

## Context and Requests <!-- HUMAN -->

LivePoll is a live poll app. The components are fixed:
nginx, a BFF, the API, the Worker, the Simulator, the frontend, Ruvio and Postgres.
- R1: The whole system starts with one command: docker compose up.
- R2: The API and the Worker can each run as 2 or more replicas.
      If one replica stops, the system keeps working.
- R3: Postgres is the permanent record of polls, options and votes.
- R4: Fast-changing data (counters, who has voted, the work queue,
      live result updates, business metrics) lives in Ruvio.
- R5: Every accepted vote reaches Postgres within 5 seconds.
- R6: The browser never talks to Ruvio or Postgres directly.

## Out of Scope <!-- HUMAN -->

- More services than the components listed above
- Any other database or a message broker such as RabbitMQ
- Kubernetes or any cloud deployment
- Multi-region deployment

## Options and Decision <!-- AI PROPOSES -->

| Option | Data path | Result |
| --- | --- | --- |
| A. API writes Postgres on the vote call | The API inserts the durable vote during the request. Ruvio holds counters, sessions, and live messages. | Leaves R4's work queue unused. docs/architecture.md assigns the durable vote to the Worker. |
| B. Accept in Ruvio, Worker writes Postgres | The API records acceptance only in Ruvio. A Worker replica reads the queue and inserts Postgres. | Meets R1–R6 with the fixed components. |
| C. API writes both stores | The API inserts Postgres and enqueues Ruvio on the same call. The Worker repairs gaps. | The API stores the durable vote, which docs/architecture.md assigns to the Worker. A crash between the two writes leaves the queue and Postgres different. |

Decision: Option B. `docker compose up` starts nginx, the BFF, the Frontend, the Simulator, Postgres, Ruvio, and at least two API replicas and two Worker replicas. The browser and the Simulator enter through nginx and the BFF. The API accepts a vote by writing Ruvio. A Worker stores that vote once in Postgres under the username.

Reasons:

1. R1. One Compose project starts the fixed components and no others. The API and the Worker each have at least two replicas, which docs/architecture.md already requires.
2. R2. Vote state sits in Ruvio and Postgres, outside either process, so any replica can take the next call. The dev token is a stateless JWT, so there is no session to share. Workers share one stream consumer group. `XREADGROUP` reads that stream with the `>` id and without `NOACK`, so a read stays pending until `XACK`. An entry still pending after a replica stops is claimed by a live replica with `XCLAIM` or `XAUTOCLAIM` after 1000 ms, the min-idle time ADR-0002 sets. Those commands are the claim path. Ruvio does not move a pending entry on its own.
3. R3. Postgres stores polls, options, and each accepted vote once under the voter's username. That row is the durable copy.
4. R4. Ruvio holds the counters, the who-has-voted mark, the stream that is the work queue, live result snapshots, and the business metrics (votes per second, total votes, active polls, votes per option, and the worker backlog). The who-has-voted mark is a string written with `SET` `NX` and without `EX`, `PX`, `EXAT`, or `PXAT`. `ONCE` keeps its key only until the TTL, and a later call does not refresh that TTL, so it cannot represent a vote that stays closed for the life of the results. The backlog figure is the `XPENDING` count. `XACK` leaves the stream entry stored, and `XLEN` still counts it.
5. R5. The API appends the accepted vote with `XADD` and does not set `DELAY`, so the entry is not hidden from `XREADGROUP`. The Worker inserts the vote in Postgres and only then acknowledges it. Five seconds is that Worker deadline. The same username is stored once when the entry is delivered again, because `IDEM` is not exactly-once for external effects. A vote accepted as the 10-minute window ends is still stored.
6. R6. Sign-in, voting, results, and metrics go from the browser to nginx, then the BFF, then an API replica. The API and the Worker are the components that open Ruvio and Postgres.

Ruvio, as the project notes state it: image `salihcantekin/ruvio:latest` or `salihcantekin/ruvio:linux`, port 6379, a password via `RUVIO_REQUIREPASS` (container images need that or an ACL password), `RUVIO_DURABILITY=everysec`, data directory `./data`. This project runs one shard (`shards` defaults to 1).

Live snapshots carry the current totals. One Worker replica publishes them on a classic channel about once a second, and every API replica subscribes and forwards to the browsers connected to it. The publisher holds a `LEASE`. Other Worker replicas call `LEASE`. While it is held the call returns a null bulk. Later grants stay larger after expiry, `RELEASE`, `DEL`, a restart, a failover, or a slot move, and the replica that receives the next token publishes. A Worker process stopping is not in that list. The browser transport stays the one docs/architecture.md leaves unnamed.

## Consequences and Risks <!-- AI PROPOSES -->

- `everysec` acknowledges after the bytes reach the kernel and runs `fdatasync` every `sync_interval_ms` (default 1000). That interval is the loss window. An acceptance still inside the window, and not yet inserted in Postgres, can be missing after a Ruvio stop. `memory` keeps nothing on disk. This project sets `everysec` and does not set `snapshot`. `always` fsyncs a client batch before the replies; this decision keeps the stated `everysec` profile.
- The notes state that `everysec` and `always` write a log under the data directory, and that `PUBLISH` is not written to the WAL. This record requires the vote stream, `ONCE` markers, and poll end-time keys to survive an `everysec` restart after the sync. Counters may lose the 1 second window and are not recounted. A missing `ONCE` marker before the Postgres row exists can allow another acceptance for that username. The Worker still stores that username once.
- `PUBLISH` is not stored. A durability profile does not keep live messages, and a later subscriber does not receive a missed snapshot. The next snapshot from a live publisher is what a reconnected API replica can forward.
- A full subscriber queue disconnects that subscriber. The C# client opens the subscribe socket again and subscribes again. The command that discovered the drop is not sent again, so a snapshot in that gap is not retried by the client.
- commands-index.md says the commands page says other shards do not receive `PUBLISH`. pubsub.md says this project runs one shard and that classic `PUBLISH` reaches subscribers on every shard in both port modes. This decision stays on one shard, where the API replicas subscribe.
- `XACK` leaves the entry stored. The vote stream does not use `MAXLEN` or `MINID`. The Worker trims or deletes an id only after Postgres has that vote. Until then the stream keeps the entry.
- `XREADGROUP` `BLOCK` holds the connection until it returns. The notes say to use a separate client for that wait. A blocked read on the only client would stall the replica's other Ruvio calls and can miss the five-second insert.
- Claim of a dead consumer waits 1000 ms, the min-idle time ADR-0002 passes to `XCLAIM` or `XAUTOCLAIM`. R5 during a Worker stop holds when that idle time plus the Postgres insert fit in five seconds. The publish lease is shorter than one second (ADR-0003).
- `LEASE` returns a null bulk while the lease is held. A replica publishes only after it receives a token. A stopped holder does not publish a final snapshot. The next publisher is the Worker that next receives a token (ADR-0003).
- WAL-backed profiles exclusively lock `<data_dir>/LOCK` before listening. One Ruvio process owns that directory. R2 asks for extra API and Worker replicas, and it does not ask for a second Ruvio process.
- The Docker example publishes port 6379 for other containers. A browser that can open that published port, or the Postgres port, would bypass nginx. Compose for this decision keeps the browser on nginx. The app pages do not call Ruvio or Postgres.
- Ruvio facts above are from `references/docker.md`, `commands-index.md`, `streams.md`, and `pubsub.md`. Where those files are silent, the Resolved questions and the Verification tests are the rule. This record does not add a command those files do not list.
- The five-second Postgres bound is an application deadline. No Ruvio timer writes Postgres. The one-second result bound is ADR-0003.

## Resolved questions

- Q4. A vote accepted as the window closes is still stored. Recorded in ADR-0002 R5.
- Q5. After the `everysec` sync, a restart keeps accepted votes still in the stream, `ONCE` markers, and poll end-time keys. Live snapshots are never stored. Business metrics may lose up to 1 second and are not recounted. Dev tokens are stateless. Recorded here and in ADR-0004 and ADR-0005.
- Q10. The publish lease and the browser reconnect are ADR-0003. The claim wait for a dead vote consumer is 1000 ms, from ADR-0002.

## Verification <!-- AI PROPOSES, HUMAN CHECKS -->

| Request | Test name | What the test proves |
| --- | --- | --- |
| R1 | ComposeUp_StartsWholeSystem | From a clean machine, `docker compose up` alone is enough to create a poll, cast a vote, and view results. |
| R2 | ApiAndWorker_AtLeastTwoReplicas | After startup, at least two API replicas and two Worker replicas are running. |
| R2 | ReplicaStop_SystemKeepsWorking | After one API replica and one Worker replica stop, a new vote is still accepted, stored once, and shown as a new total to a viewer. |
| R3 | Postgres_IsPermanentRecord | Polls, options, and each vote under that username are rows in Postgres and are still there after the API and Worker processes stop. |
| R4 | Ruvio_HoldsFastChangingData | Counters, who has voted, the work queue, live result updates, and the business metrics are served from Ruvio for any replica. |
| R5 | AcceptedVote_InPostgresWithinFiveSeconds | Each vote the API accepts, including one whose first Worker replica stops before the insert, is one Postgres row for that username within 5 seconds. |
| R4 | RuvioRestart_KeepsStreamOnceAndEndTime | After the `everysec` sync, a restart still has the unstored vote stream entry, the `ONCE` marker, and the poll end-time key. A live snapshot from before the restart is gone. |
| R6 | Browser_TalksOnlyToNginx | Sign-in, voting, results, and metrics traffic from the browser goes to nginx and does not open Ruvio or Postgres. |
