# ADR-0004: Edge and scaling

Status: Accepted
Date: 2026-11-06

## Context and Requests <!-- HUMAN -->

- R1: There is one entry point. nginx serves the frontend and forwards API calls.
- R2: Requests are spread across all API replicas.
- R3: A live results stream stays open for at least 1 hour and its events
      are not delayed by any proxy.
- R4: Rate limits apply per user, not per IP address.
      (All simulated users come from the same machine.)
- R5: A user gets a signed dev token by sending only a user name. No password.
- R6: The API and the Worker keep no state in memory that another replica needs.
- R7: Ruvio and Postgres are not reachable from outside the Docker network.

## Out of Scope <!-- HUMAN -->

- TLS certificates for a real domain
- A real identity provider, passwords or OAuth
- A CDN or a web application firewall
- Kubernetes

## Options and Decision <!-- AI PROPOSES -->

| Option | Entry and spread | Identity, limits, and data stores | Fit |
| --- | --- | --- | --- |
| 1. nginx, YARP BFF, Ruvio | nginx is the only published process. It serves the frontend and forwards API calls to the BFF. The BFF lists every API replica as a destination and sends each API call to one of them, with session affinity off. It sends `/admin/simulations/*` to the Simulator. | The API signs a stateless dev token from the user name. A name in `Admin__Users` gets `role=admin`. The Ruvio rate-limit middleware partitions by that user name and fails closed. Ruvio and Postgres publish no host port. | Meets R1–R7 and the stack in architecture.md: nginx in front of a YARP BFF, shared state in Ruvio and Postgres. |
| 2. nginx spreads API calls | nginx serves the frontend and spreads API calls across the API replicas. There is no BFF. | Same signed user-name token, same Ruvio partition, same unpublished data stores. | Architecture assigns spreading to the BFF. This option gives that job to nginx. |
| 3. Address limit and process memory | nginx is the only entry. It applies its limit by client address. Each API replica keeps the session and the limit counter in its own memory. | A user name still receives a token, and the token is honored only by the process that created it. | Every simulated user shares one address, so they share one limit. The session and the counter are invisible to the other replicas. |

Decision: option 1.

nginx is the only Compose service with a published port. It serves the frontend files. It forwards API calls, including the Simulator's public votes, to the BFF. The BFF is the YARP component in architecture.md. It listens only on the Docker network, where nginx can reach it.

Each API replica is its own BFF destination for API calls. Session affinity is off. One API call goes to one destination, and the destination set is the full replica set (at least two, per NFR-6), so those calls reach every replica. The Worker is not a destination. It takes accepted votes from Ruvio. `/admin/simulations/*` is not an API destination. The BFF sends that path to the Simulator and checks `role=admin` there.

Live results follow the SSE path in architecture.md: one Worker publishes a snapshot, Ruvio delivers it to every API replica, and that replica writes it to the browsers connected there. On that route, nginx disables response buffering and compression and sets the proxy read timeout to at least 1 hour. The BFF leaves that response unbuffered and keeps the route open for at least 1 hour. Each event passes through as the API writes it.

Rate limits run in the API, using `Ruvio.AspNetCore.RateLimiting` on the .NET 10 API. That package is middleware over Ruvio `LIMIT`, not the in-process ASP.NET Core `RateLimiter`. The API calls `AddRuvioClient` first; the middleware uses that client and does not own or dispose it. `UseRuvioRateLimiting` runs after routing, authentication, and authorization, so the user name claim is already present. `PartitionKey` is that claim. The vote route carries one fixed-window policy. Fixed window is the documented default and is first-request-anchored. A full window answers 429 at once and does not queue. The Simulator does not retry that 429. Permit count and window length are host configuration for `LimitFollowsUserName`; this record fixes the algorithm and the partition, and that test supplies the finite numbers. Outage mode is `FailClosed`. When Ruvio is unavailable the vote route returns 503 and does not accept the vote, because acceptance needs Ruvio anyway. Sign-in and the public results stream have no limit policy. Abuse protection for those two routes is out of scope in docs/requirements.md. An endpoint with no policy is not limited.

Sign-in sends a user name and no password. The API returns a stateless JWT signed with a key that configuration gives to every API replica. There is no server session to store. `Admin__Users` is a comma-separated configuration list of admin usernames. When the user name is in that list, the token includes `role=admin`. Every `/admin/*` route requires that role. Real role management is out of scope. There is no maximum user-name length; the name must not be blank or whitespace. A request that needs a user name and has none is rejected. Host start fails when `AddRuvioRateLimiting` was not called, the named client is missing, or an endpoint names an unknown policy.

Vote acceptance, the queue, locks, counters, metrics, rate-limit budgets, and live snapshots stay in Ruvio or Postgres, as the architecture data-placement table says. The dev token is not among them. An API or Worker process keeps nothing in memory that another replica must read. Each API may cache a poll end time after its first read; any replica can read that key from Ruvio again. The signing key is shared configuration, the same value on every API replica.

Ruvio and Postgres join the Compose network and publish no host port. The API and the Worker reach them by Compose DNS. The Ruvio container gets `RUVIO_REQUIREPASS`, because the docker notes say a container image needs that or an ACL password. `RUVIO_DURABILITY=everysec` and the default data directory stay as those notes set them for this project. The sample `docker run -p 6379:6379` mapping is left unused.

## Consequences and Risks <!-- AI PROPOSES -->

- Every API call crosses nginx and then the BFF. Vote p95 under 100 ms is the accept path in ADR-0002, proved by the 1,000-voter load test.
- The SSE response stays on the API replica that accepted it. Stopping that replica ends that stream. ADR-0003 answers what happens next: the page opens a new stream by itself, and another Worker publishes after the short lease.
- Ruvio facts here come from `.cursor/skills/ruvio/references/rate-limiting.md` and `.cursor/skills/ruvio/references/docker.md`.
- The vote route is one fixed window partitioned by username. Permit count and window length are the configuration `LimitFollowsUserName` sets. nginx and the BFF timeouts for the results stream are the one-hour unbuffered path in this ADR.
- The rate-limiting package targets .NET 8. This API is .NET 10 and uses that package. `RateLimitPackage_LoadsOnNet10` proves it loads.
- The signed user name is the trusted partition. There is no maximum length. A missing user name on a route that needs one rejects the request. Host start checks registration, the named client, and the policy name.
- An endpoint with no policy is not limited, and a 404 is never limited. Sign-in and the results stream stay unlimited so the Simulator can sign in 1,000 users from one machine and anyone with the link can watch (FR-8). Abuse protection for those routes is out of scope.
- The fixed window is first-request-anchored, so calls at the window boundary can receive two windows of permits. Several limits on one policy each send a `LIMIT` and are not atomic; the vote route has one limit. Eviction, data loss, deletion, and failover can reset a budget. This is not a billing ledger.
- Outage mode is `FailClosed`. A vote while Ruvio is unavailable returns 503 and is not accepted. The middleware never retries a `LIMIT` that may already have been applied. A full window is 429 at once, with no waiting queue and no Simulator retry.
- Host start fails when `AddRuvioRateLimiting` was not called, the named client is missing, or an endpoint names an unknown policy. That startup check does not open a connection, so a successful start does not show that Ruvio is reachable.
- `everysec` acknowledges after the bytes reach the kernel and runs `fdatasync` every `sync_interval_ms` (default 1000). That interval is the loss window. There is no session key. `PUBLISH` is not written to the WAL, so live snapshots are not stored. Which other data survives a restart is ADR-0001: the vote stream, `ONCE` markers, and poll end-time keys.
- The docker notes' example publishes port 6379, and they say other containers open that published port. They do not describe a Compose file that omits `ports`. This ADR leaves 6379 unpublished. Container images set `RUVIO_PROTECTED_MODE=false`, so `RUVIO_REQUIREPASS` stays set. The notes also allow `RUVIO_ALLOW_UNAUTHENTICATED=true` on a network that is already private; the running setting in this ADR is `RUVIO_REQUIREPASS`.
- The Ruvio notes do not mention Postgres. Postgres stays unpublished because R7 requires it. A later `ports` entry on either service would expose that service on the host.
- Every API replica needs the same signing key, so a token from one replica validates on another. The key can mint any user name. Passwords, OAuth, and a real identity provider are out of scope.
- Limit keys store a hash of the partition and the policy. Raw user names are not stored in those keys. The dev token is stateless, so Ruvio does not hold a session.
- Each API replica is its own BFF destination for API calls, so another replica means another destination entry. `/admin/simulations/*` is a Simulator destination. One BFF is what architecture.md draws. While that process is stopped, nginx can still serve the frontend and API calls have no BFF to reach.
- The frontend is React, Vite, and TypeScript (ADR-0006). nginx serves the static files that build produces.
- `LimitFollowsUserName` sets a finite permit count and window in configuration. The product rule is one fixed window per username.

## Resolved questions

- Q1. The frontend is React, Vite, and TypeScript. Recorded in ADR-0006.
- Q3. `Admin__Users` lists admin usernames. A dev token for one of them includes `role=admin`. Every `/admin/*` route requires that role. Recorded in this ADR (R5).
- Q5. There is no server session. Restart survival for votes, `ONCE` markers, and end-time keys is ADR-0001. Live snapshots are not stored.
- Q6. The BFF routes `/admin/simulations/*` to the Simulator and checks `role=admin` there. Recorded in this ADR (R2).
- Q10. Stopping the viewer's API replica ends that stream. ADR-0003 records the reconnect and the next publisher.
- Outage mode is `FailClosed`. A vote returns 503 when Ruvio is unavailable.
- Sign-in and the results stream stay unlimited. Abuse protection for them is out of scope.

## Verification <!-- AI PROPOSES, HUMAN CHECKS -->

| Request | Test name | What the test proves |
| --- | --- | --- |
| R1 | OnlyNginxIsPublished | From outside the Docker network, only nginx accepts a connection. That port serves the frontend, and an API call on it arrives at the BFF. |
| R2 | CallsReachEveryApiReplica | API calls sent through nginx are handled by every running API replica. |
| R3 | ResultsStreamHoldsOneHour | A live results stream opened through nginx and the BFF is still open after 1 hour. |
| R3 | ResultsEventsFlushImmediately | A results event written by the API reaches the client while the stream is still open, and neither proxy holds it until the response ends. |
| R4 | LimitFollowsUserName | From one client address, the user who exceeds the configured fixed window receives 429, and a second user name still receives a normal response. |
| R5 | TokenFromUserNameOnly | A sign-in request that contains only a user name returns a signed stateless token, and the call succeeds with no password. |
| R5 | AdminUsers_ReceiveRoleAdmin | A user name listed in `Admin__Users` receives `role=admin` on the token. A name that is not listed does not. |
| R5 | AdminRoutes_RequireRoleAdmin | `/admin/simulations/*` through the BFF reaches the Simulator only when the token has `role=admin`. Any other token is forbidden, and the API does not handle the route. |
| R4 | VoteLimiter_FailClosed_Returns503 | When Ruvio is unavailable, the vote route returns 503 and does not accept the vote. |
| R4 | RateLimitPackage_LoadsOnNet10 | The .NET 10 API host starts with `Ruvio.AspNetCore.RateLimiting` registered. |
| R6 | OtherReplicaCompletesTheVote | One API replica accepts a vote, a different Worker replica stores that vote once, and a different API replica accepts a later call with the same stateless token. |
| R7 | DataPortsRefuseOutside | A client outside the Docker network cannot connect to Ruvio or Postgres. A container on that network can reach both. |
