# ADR-0003: Live results

Status: Accepted
Date: 2026-11-06

## Context and Requests <!-- HUMAN -->

People watch a poll page while up to 1,000 simulated users vote.
Simulated voters only send votes; they do not watch results.
- R1: A new vote shows up on every viewer's screen within 1 second.
- R2: This works when the vote and the viewer are on different API replicas.
- R3: If the API replica a viewer is connected to stops, the page reconnects
      by itself and keeps showing live results. No manual refresh.
- R4: The browser never connects to Ruvio.
- R5: Each poll sends at most one result update every 1 second to each viewer,
      no matter how many votes arrive.
- R6: Only one Worker replica publishes results at a time. A replica that was
      paused for a long time cannot publish old results after it wakes up.
- R7: Use the newest .NET 10 APIs for live updates.

## Out of Scope <!-- HUMAN -->

- WebSocket or SignalR
- Sticky sessions or IP-based routing
- Live results for simulated voters
- Mobile push notifications
- Showing the full vote history after a reconnect

## Options and Decision <!-- AI PROPOSES -->

| Option | What it does | Fit |
| --- | --- | --- |
| 1. Fenced publisher, classic pub/sub, SSE | One Worker holds a fenced lease and publishes at most one full totals snapshot per poll every 1 second with classic `PUBLISH`. Every API replica subscribes and forwards that snapshot on the .NET 10 Server-Sent Events response. | Matches the live-results flow and R1–R7. |
| 2. Each API reads shared totals | Every API replica reads Ruvio totals itself and writes them on its own SSE response. No Worker publishes. | Shared totals can satisfy R2. R6 fails: there is no single publishing Worker, and a replica can send totals it read before a long pause. |
| 3. Snapshot stream | A Worker appends snapshots with `XADD`. API replicas read them with `XREAD` or `XREADGROUP` and write SSE. | Those commands are for the vote queue. `XREADGROUP` delivers an entry to one consumer, so the other API replicas miss it. The entry stays stored (`XACK` leaves it, and `XLEN` still counts it), so a later reader can see older snapshots. That history is out of scope. Classic `PUBLISH` is the live path, and it is not stored. |

Decision: option 1.

The browser opens the results response on the public path: the page, nginx, the BFF, and one API replica. That is the browser's results connection, so the browser never connects to Ruvio (R4). Simulated voters stay on the public vote API and do not open this response.

The API records each acceptance in Ruvio. Live totals and live snapshots are in Ruvio. Postgres keeps the durable vote and does not carry live messages. The publishing Worker reads the current shared totals, including votes accepted and not yet stored, and publishes that picture. An accepted vote can therefore be on screen within one second without waiting for the durable write (R1).

For each poll whose totals changed, the holder publishes immediately when that poll has had no snapshot in the past 1 second. When one was just sent, the next snapshot waits until 1 second after it. A vote that arrives just after a snapshot is sent on the next snapshot (R1). The picture is the current totals and votes per second for that poll, so many votes become one update (R5). This project runs one shard. Classic `PUBLISH` reaches subscribers on every shard, so every API replica receives the snapshot, including the replica that did not take the vote (R2).

`Fenced` is on for every Worker, and this lock name is only used as a fenced lock. The publish loop calls `TryAcquireAsync` with a lease shorter than one second. Null means the lock stayed taken, and that replica does not publish. `AutoRenew` stays on and renews every third of the lease. A crash holds the work for at most one lease, and another replica can publish inside the viewer's one-second budget. `AddRuvioSingleton` defaults to a 30-second lease and a 5-second retry, so the loop passes the short lease on `TryAcquireAsync` instead of keeping that default.

The snapshot carries the holder's fencing token. A later owner always gets a larger token, and `RENEW` does not change it. `PUBLISH` does not check the token. Each API drops a snapshot whose token is smaller than the newest token it has already accepted for that poll, and it forwards at most one snapshot per poll per viewer every 1 second. `PausedReplicaCannotPublishOldSnapshot` proves the drop. A replica that wakes after a pause longer than the lease finds `IsHeld` false, because `RENEW` returns 0 when the token is expired, released, or not the holder. It does not publish the picture it built before the pause (R6). If the lease is still held, it discards that picture and reads the totals again before it publishes.

When the viewer's API replica stops, its response ends. The page opens a new results response by itself, with no refresh (R3). The BFF spreads that new call to any API replica that is still up. Replicas subscribe at startup. A dropped subscribe socket is opened again and the channels are subscribed again when `Reconnect` is true, which is the default. `PUBLISH` is not stored and is not written to the WAL, so the missed gap is not replayed. The next snapshot is the current totals. The page keeps the last totals on screen and applies that snapshot. There is no vote-history payload.

R7 is the .NET 10 Server-Sent Events response for that call. The architecture names SSE as the hop from each API replica to the browsers connected there. `LiveResultsUseDotNet10Sse` proves that response, and the host serves neither WebSocket nor SignalR.

## Consequences and Risks <!-- AI PROPOSES -->

1. Each API replica keeps the client's second TCP socket for `SUBSCRIBE`. `GET`, `SET`, and `PUBLISH` stay on the command socket. `Reconnect` stays at the default true so the new socket subscribes again.
2. The holder renews a lease shorter than one second, every third of that lease, for as long as it publishes. The 30-second default would leave no publisher for longer than R1 allows after a crash.
3. A vote that arrives just after a snapshot waits until 1 second after that send. A `PUBLISH` that finds a dropped socket is not sent again, and the next read of shared totals is the recovery.
4. A full subscriber queue disconnects that subscriber. The notes give no queue length, and this design does not need one: that API replica subscribes again, missed publishes are not replayed, and the next snapshot is the current totals.
5. If a failover loses the lease key, two owners can overlap for up to one lease. Both publishes are delivered. The API drops the smaller fencing token, or the old owner can replace a newer snapshot.
6. `IsHeld` stays true until a whole lease passes with no renewal. A pause shorter than the lease is still the holder. The wake path discards any snapshot built before the pause. The lease does not do that by itself.
7. nginx and the BFF flush each SSE event as the API writes it, which ADR-0004 requires. A proxy that held the body until the handler returned would show no live results, so that configuration stays as ADR-0004 sets it.
8. The results call is the .NET 10 Server-Sent Events response. `LiveResultsUseDotNet10Sse` is the check. The host serves neither WebSocket nor SignalR.
9. Live messages are absent from the WAL, so a Ruvio restart does not replay them. After the clients subscribe again, viewers catch up on the next full snapshot. There is no history response for the gap.

## Resolved questions

- Q2. The browser transport is Server-Sent Events. Recorded in this ADR (R7).
- Q9. The publisher sends at most one snapshot per poll every 1 second. Recorded in this ADR (R1, R5).
- Q10. The publish lease is shorter than one second, so another Worker publishes after the holder stops. When the viewer's API replica stops, the page opens the stream again by itself. Recorded in this ADR (R1, R3, R6).
- `PUBLISH` does not check the fencing token, and the subscriber queue length is not a product setting. This ADR keeps the API drop of a smaller token and the resubscribe path. `PausedReplicaCannotPublishOldSnapshot` and `PageReconnectsAfterApiReplicaStops` prove them.

## Verification <!-- AI PROPOSES, HUMAN CHECKS -->

| Request | Test name | What the test proves |
| --- | --- | --- |
| R1 | AcceptedVoteVisibleWithinOneSecond | With a results page open, one accepted vote is on screen within 1 second. |
| R1 | PublishingWorkerStopStillWithinOneSecond | After the Worker that holds the lease stops, a viewer still sees a new total within 1 second. |
| R2 | VoteOnOneReplicaVisibleOnAnother | A vote accepted on one API replica updates a viewer whose results response is on another API replica. |
| R3 | PageReconnectsAfterApiReplicaStops | When the API replica holding the viewer's response stops, the page opens a new results stream by itself and shows a later snapshot, with no manual refresh. |
| R4 | BrowserNeverConnectsToRuvio | While a viewer watches live results, every browser connection is to the public site and none is to Ruvio. |
| R5 | OneSnapshotPerPollEverySecond | A burst of votes on one poll produces a gap of at least 1 second between results events for that poll on the viewer's stream. |
| R1 | VoteJustAfterSnapshotVisibleWithinOneSecond | A vote accepted just after a snapshot is on the viewer's stream within 1 second. |
| R6 | OnlyLeaseHolderPublishes | With two Worker replicas running, only the replica that holds the fenced lease publishes. |
| R6 | PausedReplicaCannotPublishOldSnapshot | A publisher paused longer than the lease does not publish the snapshot it built before the pause, and a smaller fencing token does not replace a newer snapshot. |
| R7 | LiveResultsUseDotNet10Sse | The results response is the .NET 10 Server-Sent Events API, and the host serves neither WebSocket nor SignalR. |

Every R must have at least one test.
