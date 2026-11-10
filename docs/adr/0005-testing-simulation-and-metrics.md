# ADR-0005: Testing, simulation and business metrics

Status: Accepted
Date: 2026-11-06

## Context and Requests <!-- HUMAN -->

- R1: Tests are written before the code they test (TDD).
      Tests are committed before the implementation.
- R2: Line coverage of the Domain and Application layers is at least 80%.
      The build fails below that.
- R3: Integration tests use real Postgres and real Ruvio in containers, not mocks.
- R4: A simulator creates virtual voters. We can set the number of voters,
      how fast they join, how long they vote and how votes are split between options.
- R5: The same simulation settings with the same seed give the same votes.
- R6: The simulator uses only the public API through nginx, like a real user.
- R7: The simulator handles 1,000 voters on one machine.
- R8: A side panel shows business metrics that update at least every second:
      votes per second, total votes, active polls, votes per option,
      and how many votes are waiting to be written to Postgres.
- R9: Business metrics are stored in Ruvio.

## Out of Scope <!-- HUMAN -->

- Technical metrics, tracing or telemetry dashboards
- Browser end-to-end UI tests
- Running the simulator on another machine

## Options and Decision <!-- AI PROPOSES -->

| Option | Approach | Result |
| --- | --- | --- |
| A | Integration tests mock Postgres and Ruvio. The simulator calls the API in process. The five business numbers are rows in Postgres. | Breaks R3, R6, and R9. The architecture gives the Simulator no private path and places business metrics in Ruvio. |
| B | Domain and Application tests are committed before the code they cover, and the build fails when either layer is under 80% line coverage. Integration tests start real Postgres and real Ruvio containers. The .NET Simulator builds a seeded vote plan and sends sign-in and votes only through nginx. The five business metrics are Ruvio counters, and the side panel reads them at least once a second. | Meets R1–R9 with the architecture already set: .NET 10 Simulator, nginx in front of the BFF, Postgres for the durable vote, Ruvio for business metrics. |
| C | An external load tool creates the voters. A telemetry dashboard stores and shows the five numbers. | Breaks R9. A technical metrics dashboard is out of scope. The architecture already names a .NET Simulator that enters through nginx. |

Decision: Option B.

1. R1 and R2 apply to the Domain layer and the Application layer. A failing test is its own commit. The implementation of that behavior is a later commit. Coverlet measures line coverage of each layer on its own and fails the build when either result is below 80%, which is NFR-9. Those unit tests may use in-memory fakes of ports. A test that talks to Postgres or Ruvio is an integration test and is outside that coverage count.
2. R3 uses Testcontainers. The fixture starts a Postgres container and a Ruvio container. The Ruvio container is `salihcantekin/ruvio:latest` or `salihcantekin/ruvio:linux`, publishes port 6379, sets `RUVIO_REQUIREPASS`, and sets `RUVIO_DURABILITY=everysec`. The data directory stays the default `./data`. The fixture does not replace either service with a mock. `docker compose up` remains the way a person starts the whole system (NFR-8). The test fixture starts only the containers that test needs.
3. R4 and R5 belong to the .NET 10 Simulator. The settings are voter count, ramp-up, duration, per-option distribution, and a seed. Join time is a pure function of the voter index, the count, and the ramp-up. The username is a pure function of the seed and the index. The option is a pure function of the distribution and the seed. Duration is how long that plan keeps voting. The same settings and the same seed therefore list the same votes. A repeated run uses a new poll, because a second vote from the same username on the same poll is refused.
4. R6 and R7 stay on the public path in the architecture diagram. Each virtual voter signs in and votes by HTTP to nginx only. The Simulator has no Ruvio client and no Postgres client. It runs in its own container. Voters are async tasks, not threads. They share one `HttpClient` backed by `SocketsHttpHandler` with a connection limit, and a ramp-up overlaps them. One Simulator process on one machine runs 1,000 virtual voters at the same time, which is NFR-2. The load test does not add a second machine. That same run is the p95 proof for ADR-0002. If p95 is over 100 ms, add API replicas.
5. R8 and R9 serve the panel from the API. The Frontend side panel polls that API through nginx at least once a second. The poll interval is what the panel test checks. There is no browser end-to-end test and no metrics event stream. Total votes, votes per option, and the worker backlog are counter keys. The API lists each active poll's options and reads that option's counter. Active polls are a Ruvio sorted set scored by end time. Votes per second are not a counter. The publishing Worker computes them from vote-stream entry ids, which carry Ruvio server time.
6. On acceptance the API applies `INCR` to total votes, to that option's count, and to the worker backlog. It does not bucket the vote by the API clock. The publishing Worker counts stream entry ids whose Ruvio server time falls in the previous second, and that count is the votes-per-second figure on the results page and the metrics panel. The Worker applies `DECR` to the backlog when it stores that vote once in Postgres. There is no scheduled close job. On create, the poll's id is added to the active-poll sorted set with its end time as the score. The active count is the number of entries whose end time is after now. The publishing Worker removes entries whose end time is at or before now.
7. The backlog is that counter, not the stream length. `XACK` leaves the stream entry stored, so `XLEN` still counts votes already written to Postgres. The `XPENDING` summary count is only the delivered, not-yet-acknowledged set. `XINFO GROUPS` also reports `lag`. This record does not use `lag`. The backlog shown on the panel is the counter. The vote queue's consumer group is the one ADR-0002 sets.
8. `PUBLISH` is not the metrics store. It is not stored and it is not written to the WAL, and the commands page says other shards do not receive it. `LIMIT` is not the votes-per-second store either: a full window returns a null bulk and does not count the rejection, and `GET` of a limit is `WRONGTYPE`. Votes per second come from stream entry ids, as item 6 says.

## Consequences and Risks <!-- AI PROPOSES -->

- Ruvio facts above are from `references/commands-index.md`, `references/docker.md`, and `references/streams.md`. The active-poll sorted set is the rule in item 6. `ActivePolls_CountByEndTime` proves the count. This record does not add a command name those files do not list.
- Those files do not describe a time-series or histogram command. Votes per second are a count of stream entry ids for the previous Ruvio server second, computed by the publishing Worker.
- Counter reads of total votes, votes per option, and the backlog are proved by `BusinessMetricsStorageTests.FiveCountersAreReadFromRuvio`. A missing key is zero for those three. Votes per second do not use a per-second key.
- Those files do not describe a transaction that updates several keys together. One acceptance writes total, option, and backlog as separate commands, so a reader can see a partial update. `INCRBY ... MAX` is not used. That form returns a null bulk and writes nothing when the result would pass the limit.
- `DECR` is a signed 64-bit decrement, so a second decrement drives the backlog negative, and a missed decrement leaves it high. `IDEM` does not by itself make the metric decrement safe. The Worker decrements once, on the single store of that vote.
- Active polls have no scheduled close. The publishing Worker removes sorted-set entries whose end time is at or before now. The count is the entries still after now. A missed removal leaves the active count high until the next publish pass.
- `everysec` acknowledges after the bytes reach the kernel and runs `fdatasync` every `sync_interval_ms` (default 1000). That interval is the loss window. Business metrics are not rebuilt from Postgres after a restart. A crash inside the window can drop counters while the durable votes remain in Postgres, and the panel can then disagree with Postgres. That drift is accepted. There is no recount. A test container that omits `RUVIO_DURABILITY` follows the file default `memory`, which keeps nothing on disk, so the fixture sets `everysec` on purpose. Container images also need `RUVIO_REQUIREPASS` or an ACL password.
- Votes per second use Ruvio server time on the stream entry id, so two API clocks do not split the figure.
- Integration tests need a container runtime and are slower than mocks. The R6 fixture must expose only the nginx public URL to the Simulator. A fixture that bypasses nginx can pass while the compose path is still wrong.
- The TDD order check reads separate commits. A squash merge of the failing test into the implementation removes the evidence R1 asks for. The check has to run on the branch commits that still show that order. The check applies to Domain and Application production changes, not to docs-only commits.
- Domain is `src/LivePoll.Domain`, tested by `tests/LivePoll.Domain.Tests`. Application is `src/LivePoll.Application`, tested by `tests/LivePoll.Application.Tests`. Coverlet measures each assembly on its own and fails the build when either line coverage is under 80%.
- One Simulator container holds 1,000 in-flight async voters on one shared `HttpClient`. Vote p95 is the Ruvio-only path in ADR-0002. If `SimulatorLoadTests.OneThousandConcurrentVotersOnOneMachine` shows p95 at or above 100 ms, add API replicas.
- The same seed produces the same usernames. Replaying it against a poll that already has those votes is refused. R5 is proved on the plan, and a second poll is required to cast that plan again.

## Resolved questions

- Q8. One thousand virtual voters are async tasks in the Simulator container, sharing one `HttpClient` with a connection limit, and a ramp-up. Recorded in R6 and R7.
- Q11. Domain and Application live at `src/LivePoll.Domain` and `src/LivePoll.Application`, with tests in `tests/LivePoll.Domain.Tests` and `tests/LivePoll.Application.Tests`. Coverlet fails the build under 80% line coverage for either assembly. Recorded in R2.
- Q5 metrics. A restart may drop up to 1 second of business metrics. There is no recount. Recorded in the consequences above.
- Active polls are a sorted set scored by end time. There is no scheduled close job. The publishing Worker removes entries whose end time is at or before now.
- Votes per second are computed by the publishing Worker from stream entry ids (Ruvio server time).
- The vote-queue consumer group is the one ADR-0002 already sets. `lag` is not the backlog.

## Verification <!-- AI PROPOSES, HUMAN CHECKS -->

| Request | Test name | What the test proves |
| --- | --- | --- |
| R1 | `TddCommitOrderTests.TestCommitPrecedesImplementation` | For each Domain and Application production change, an earlier commit contains the failing test and does not contain that implementation. The build fails when the test and the implementation land in one commit or in the other order. |
| R2 | `CoverageGateTests.DomainAndApplicationLineCoverageAtLeast80` | Coverlet measures `src/LivePoll.Domain` and `src/LivePoll.Application` on their own. The build fails when either line coverage is below 80%, and it passes when each is at least 80%. The tests live in `tests/LivePoll.Domain.Tests` and `tests/LivePoll.Application.Tests`. |
| R3 | `VoteFlowContainerTests.PersistsThroughRealPostgresAndRuvio` | The test starts a real Postgres container and a real Ruvio container (project image, port 6379, password, `everysec`), accepts a vote, and reads the stored vote and the shared acceptance from those containers, with no mock of either. |
| R4 | `SimulatorPlanTests.CountRampDurationAndSplitShapeTheVotes` | Changing the voter count, the ramp-up, the duration, or the per-option split changes how many voters join, when they join, how long they vote, and how the votes divide across options. |
| R5 | `SimulatorPlanTests.SameSettingsAndSeedProduceTheSameVotes` | Two plans built with the same settings and the same seed list the same votes. A different seed produces a different split. |
| R6 | `SimulatorHttpTests.VotesOnlyThroughNginxPublicApi` | Each virtual voter signs in and votes only by the public API through nginx. Votes are stored even though the Simulator cannot open Ruvio or Postgres. |
| R7 | `SimulatorLoadTests.OneThousandConcurrentVotersOnOneMachine` | On one machine, the Simulator container runs 1,000 async voters at the same time through one shared `HttpClient` (`SocketsHttpHandler` with a connection limit) and a ramp-up. Each voter votes through the public API. |
| R8 | `MetricsPanelTests.ShowsFiveMetricsAndRefreshesEverySecond` | The side panel shows votes per second, total votes, active polls, votes per option, and how many votes are waiting to be written to Postgres, and it requests those values at least once a second. The test is not a browser end-to-end test. |
| R9 | `BusinessMetricsStorageTests.FiveCountersAreReadFromRuvio` | After votes are accepted, total votes, votes per option, and the backlog are in Ruvio, the API serves them, and votes per second match the publishing Worker's count of stream entry ids. |
| R8 | `ActivePolls_CountByEndTime` | Active polls are the sorted-set entries whose end time is after now. The publishing Worker removes entries at or before now. There is no scheduled close job. |
| R8 | `VotesPerSecond_FromStreamEntryIds` | The publishing Worker computes votes per second from vote-stream entry ids using Ruvio server time, not an API replica clock. |
| R9 | `MetricsDrift_AcceptedNoRecount` | After an `everysec` loss window, metrics are not rebuilt from Postgres. |
