# Functional Requirements

## Polls

- FR-2. Any signed-in user can create a poll.
- FR-3. A poll has a title and from 2 to 6 options.
- FR-4. Only an admin can add, edit, or remove options or edit the title, and a voter cannot add an option.
- FR-7. After a poll has received a vote, its title and options cannot be changed.
- FR-14. A poll cannot be created with a blank or whitespace-only title. An option cannot be blank or whitespace-only, and must be at least 3 characters.
- FR-15. A poll is open for 10 minutes.
- FR-16. A poll title is limited to 120 characters.

## Voting

- FR-5. A signed-in user can cast one vote on a poll, and that vote is stored under their username.
- FR-6. A user who has voted on a poll cannot change that vote or vote on it again.

## Live Results

- FR-8. Anyone with the correct link can view that poll’s results, and the results stay available.
- FR-9. The results page shows live vote totals, votes per second, and a live-updating chart of those totals, updating as votes arrive.

## Identity

- FR-1. A user can sign in by choosing a username and receiving a token, without a password.

## Simulation

- FR-11. On /admin, an admin can set the number of virtual users, ramp-up time, duration, and vote distribution per option.
- FR-12. On /admin, an admin can start and stop the virtual users.
- FR-13. Virtual users vote through the same public voting API as real users.

## Business Metrics

- FR-10. The metrics panel shows votes per second, total votes, active polls, votes per option, and the worker backlog.

# Non-Functional Requirements

Targets are measured on the instructor's machine. Your numbers may differ; the measuring method is what matters.

| ID | Requirement | How we verify |
|---|---|---|
| NFR-1 | With 1,000 concurrent virtual voters on one machine, vote API latency is p95 under 100 ms. | Load-test the vote API at that concurrency and record p95; pass if it is under 100 ms. |
| NFR-2 | One machine sustains 1,000 concurrent virtual voters voting through the public voting API. | On one host, start 1,000 virtual users (FR-11, FR-13) and confirm they all vote concurrently. |
| NFR-3 | An accepted vote appears on a viewer’s results screen within 1 second. | With a results page open, cast one vote and measure until the new total is visible; pass if that is within 1 second. |
| NFR-4 | Every vote the API has accepted is still stored once under that username after a worker crashes. | Accept a known set of votes, kill a worker, then recover; pass if the stored count equals the accepted count. |
| NFR-5 | The API and the Worker keep vote state outside the process, so any replica can handle any request. The dev token is stateless. | Cast a vote on one API replica and process it on one Worker replica, then repeat on the other replicas with the same token; pass if no request depends on returning to the first process. |
| NFR-6 | The API and the Worker each run at least 2 replicas. | After startup, pass if at least 2 API replicas and 2 Worker replicas are running. |
| NFR-7 | If one API replica or one Worker replica stops, viewers still receive live results within 1 second. | Stop one API replica and one Worker replica during voting; pass if a viewer still sees new totals within 1 second. |
| NFR-8 | The whole system starts with one command: `docker compose up`. | From a clean machine, run only that command; pass if creating a poll, voting, and viewing results all work. |
| NFR-9 | The Domain and Application layers each have line coverage of at least 80%. | Coverlet measures `src/LivePoll.Domain` and `src/LivePoll.Application` separately; pass if each is at least 80%, and fail the build otherwise. |

# Out of Scope

Anything not listed in this document is out of scope. Ask before building it.

- Changing a vote after it is cast — FR-6 fixes a vote once it is cast.
- Anonymous voting — FR-5 stores each vote under the voter's username.
- Multiple languages — The requirements are written for a single language.
- Email or push notifications — No requirement asks the product to contact a user later.
- Exporting results — FR-9 asks for live totals, votes per second, and a live-updating chart.
- User management screens — No requirement asks for a screen that manages users.
- Real sign-up and login (passwords, OAuth). A simple dev identity is enough.
- Mobile apps — The specified product is the web flows, including /admin.
- Technical metrics and telemetry (business metrics stay in scope) — FR-10 lists the business metrics the panel shows.
- A management screen for failed messages — FR-10 asks for the worker backlog number.
- WebSocket or SignalR — Live results use Server-Sent Events (ADR-0003).
- RabbitMQ or any other message broker — The vote queue is a Ruvio stream (ADR-0002).
- Role management — Admin usernames come from configuration. There is no screen or API to manage roles.
- Abuse protection for sign-in and the results stream — Those two routes stay unlimited.
- Multi-region deployment — The non-functional targets are measured on one machine.
