# Section 05 - Decisions and Standards: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names.

## 05_02_AdrsWithMultiTasks

### 1. ADR template and the /adr skill

**Where:** New Chat · Agent (Build)

```text
Create two files.

1. docs/adr/0000-template.md with these sections, in this order:
   - Title line: "# ADR-NNNN: <Title>"
   - Status: Proposed | Accepted, and Date
   - "## Context and Requests" — marked <!-- HUMAN -->. Requests are numbered R1, R2...
     and written as behavior, not mechanism.
   - "## Out of Scope" — marked <!-- HUMAN -->
   - "## Options and Decision" — marked <!-- AI PROPOSES -->
   - "## Consequences and Risks" — marked <!-- AI PROPOSES -->
   - "## Verification" — marked <!-- AI PROPOSES, HUMAN CHECKS -->.
     A table: Request, Test name, What the test proves. Every R must have at least one test.

2. A Cursor skill at .cursor/skills/adr/SKILL.md (name: adr, one-line description,
   disable-model-invocation: true). The skill tells the agent to:
   - Use docs/adr/0000-template.md and the number it is given (or the next free one).
   - Take "Context and Requests" and "Out of Scope" from my message, word for word.
     If they are missing, ask me. Never write or change human sections.
   - Read docs/requirements.md, docs/architecture.md, earlier ADRs and any
     relevant skill (for example the ruvio skill) before proposing.
   - Propose at most 3 options in a table, then one decision with reasons.
   - Map every request to at least one test in Verification.
   - Set Status: Proposed. Only set Accepted when I say "accept".
   - Reply with: file path, the decision in one line, open questions (max 3).

Commit with the message "docs: add ADR template and /adr skill" and push.
```

### 2. Five ADRs in one prompt

**Where:** New Chat · Multitask

```text
Write ADR-001 to ADR-005 in parallel: one subagent per ADR.
Each subagent follows .cursor/skills/adr/SKILL.md exactly, uses the number and title
below, and copies the "Context and Requests" and "Out of Scope" text word for word.
Subagents must not edit each other's files. Leave every ADR as Proposed.

=== ADR-0001: Architecture and data storage (file 0001-architecture-and-data.md)
## Context and Requests
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
## Out of Scope
- More services than the components listed above
- Any other database or a message broker such as RabbitMQ
- Kubernetes or any cloud deployment
- Multi-region deployment

=== ADR-0002: Vote acceptance and processing (file 0002-vote-processing.md)
## Context and Requests
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
## Out of Scope
- Changing a vote
- A screen for managing failed votes
- Replaying votes from history
- Any message broker other than Ruvio

=== ADR-0003: Live results (file 0003-live-results.md)
## Context and Requests
People watch a poll page while up to 1,000 simulated users vote.
Simulated voters only send votes; they do not watch results.
- R1: A new vote shows up on every viewer's screen within 1 second.
- R2: This works when the vote and the viewer are on different API replicas.
- R3: If the API replica a viewer is connected to stops, the page reconnects
      by itself and keeps showing live results. No manual refresh.
- R4: The browser never connects to Ruvio.
- R5: Each poll sends at most one result update per second to each viewer,
      no matter how many votes arrive.
- R6: Only one Worker replica publishes results at a time. A replica that was
      paused for a long time cannot publish old results after it wakes up.
- R7: Use the newest .NET 10 APIs for live updates.
## Out of Scope
- WebSocket or SignalR
- Sticky sessions or IP-based routing
- Live results for simulated voters
- Mobile push notifications
- Showing the full vote history after a reconnect

=== ADR-0004: Edge and scaling (file 0004-edge-and-scaling.md)
## Context and Requests
- R1: There is one entry point. nginx serves the frontend and forwards API calls.
- R2: Requests are spread across all API replicas.
- R3: A live results stream stays open for at least 1 hour and its events
      are not delayed by any proxy.
- R4: Rate limits apply per user, not per IP address.
      (All simulated users come from the same machine.)
- R5: A user gets a signed dev token by sending only a user name. No password.
- R6: The API and the Worker keep no state in memory that another replica needs.
- R7: Ruvio and Postgres are not reachable from outside the Docker network.
## Out of Scope
- TLS certificates for a real domain
- A real identity provider, passwords or OAuth
- A CDN or a web application firewall
- Kubernetes

=== ADR-0005: Testing, simulation and business metrics (file 0005-testing-simulation-and-metrics.md)
## Context and Requests
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
## Out of Scope
- Technical metrics, tracing or telemetry dashboards
- Browser end-to-end UI tests
- Running the simulator on another machine

When all five are done, bring every file into this checkout.

Output: a table with columns ADR, Where it ran (this computer / cloud), Decision in one line, Open questions (count).
```

### 3. Review all five together

**Where:** Agent

```text
Read all files in @docs/adr/.
1. Find contradictions between ADRs, or between an ADR and @docs/requirements.md.
2. For ADR-0002, say what happens to a vote if Ruvio restarts before it reaches Postgres.
3. List any request that has no test in Verification.

Output: three headed lists matching the questions, max 4 items each, then one line "Changes needed: yes/no".
```

### 4. Accept the ADRs

**Where:** Agent

```text
I accept all the ADRs. Commit the changes and push.
```

## 05_03_FrontendChoiceAndApiContract

### 1. Comparison canvas

**Where:** New Chat · Agent (Build)

```text
We must choose a frontend framework for LivePoll. Do not edit any project files.
Read @docs/requirements.md and @docs/adr/ first.

Our criteria, in order of importance:
1. Live-updating charts that receive data over Server-Sent Events (EventSource).
2. TypeScript.
3. Builds to static files that nginx can serve. No server-side rendering.
4. Easy to learn for .NET developers who are new to frontend work.
5. Well known to AI coding agents, so they make fewer mistakes.
6. A simple build with fast local development.

Compare: React + Vite, Vue + Vite, Svelte + Vite, Angular, Blazor WebAssembly.

Show the result as a canvas: a score matrix (1-5 per criterion) with totals,
one card per option with its strongest argument for and against,
and your recommendation at the top.
```

### 2. Side question

**Where:** Side Chat

```text
In one paragraph: what is EventSource, and does Blazor WebAssembly support it natively?
```

### 3. Record the decision as ADR-0006

**Where:** Agent

```text
/adr Frontend choice

## Context and Requests
- R1: The poll page shows live-updating charts from a Server-Sent Events stream.
- R2: The frontend is written in TypeScript.
- R3: It builds to static files served by nginx. No server-side rendering.
- R4: It is easy to learn for .NET developers new to frontend work.
- R5: AI coding agents know it well.
- R6: Local development starts in seconds.
- Human decision: React + Vite + TypeScript.
  Record the comparison from the canvas as the options.

## Out of Scope
- Server-side rendering frameworks (for example Next.js)
- A mobile app
- A design system or component library of our own
- Multiple languages

Propose only the remaining choices: the chart library and how the client
code for the API is created. Max 3 options each.
```

### 4. OpenAPI contract

**Where:** New Chat

```text
Write an OpenAPI 3.1 contract at docs/api/openapi.yaml for LivePoll.
Read @docs/requirements.md and @docs/adr/ first. The contract must follow them.

Endpoints (this list is complete; do not add others):
- POST /auth/dev-token               get a signed dev token with only a user name
- POST /polls                        create a poll (title, 2-6 options, duration)
- GET  /polls/{pollId}               poll details, including whether it has ended
- POST /polls/{pollId}/votes         vote for one option
- GET  /polls/{pollId}/results       current results
- GET  /polls/{pollId}/results/stream  live results as Server-Sent Events
- GET  /metrics                      current business metrics
- GET  /metrics/stream               live business metrics as Server-Sent Events
- POST   /admin/simulations          start a simulation
- GET    /admin/simulations/{simulationId}  simulation status
- DELETE /admin/simulations/{simulationId}  stop a simulation

Rules:
- Every endpoint except POST /auth/dev-token needs the dev token as a Bearer token.
- Errors use Problem Details (RFC 9457).
- Voting twice returns 409. Voting on an ended poll returns 409 with a different
  problem type. Rate limiting returns 429 with a Retry-After header.
- SSE endpoints return text/event-stream. Describe the event name and its data schema.
- Simulation settings: poll, number of voters, ramp-up time, duration,
  vote split per option (percent), and a seed.
- Metrics: votes per second, total votes, active polls, votes per option,
  and votes waiting to be written to Postgres.
- Tag endpoints: Auth, Polls, Votes, Results, Metrics, Admin.
- Add one example for every request and response body.

Then validate it with an OpenAPI linter run through npx and fix only real errors.

Output: the linter command, errors and warnings before and after.
```

### 5. Validate against the decisions

**Where:** Agent

```text
Check @docs/api/openapi.yaml against @docs/requirements.md and @docs/adr/.
 Find requests the contract does not support,
and anything in the contract that is out of scope.

Output: a table with columns Source (file + R#), Problem, Suggested fix. Max 8 rows. If none, only "No problems".
```

### 6. Close every open question

**Where:** New Chat · Agent (Build)

```text
Close every open question before we plan. I am answering all of them below.
Apply my answers; do not ask me anything and do not leave any question open.

Read @docs/architecture.md (section "Open questions for ADRs"), @docs/adr/,
@docs/requirements.md and @docs/api/openapi.yaml.

Already answered by an ADR - mark them resolved and point to the ADR:
- Q1 frontend framework -> ADR-0006.
- Q2 live results transport -> ADR-0003 (SSE).
- Q4 vote accepted as the window closes -> ADR-0002 R5: it is still stored.
- Q10 publisher or API replica stops -> ADR-0003 (short fenced lease, the page
  reconnects by itself). Remove the "still open" note in ADR-0004.

My answers to the open ones:
- Q3 Admin: admin usernames come from configuration (Admin__Users, comma separated).
  When a dev token is issued for one of them, the token gets role=admin.
  Every /admin/* endpoint requires that role. Real role management is out of scope.
- Q5 Ruvio restart: with everysec these must survive: accepted votes still in the
  stream, ONCE markers and poll end-time keys. Live snapshots are never stored.
  Business metrics may lose up to the 1 second loss window; no recount.
  Dev tokens are signed and stateless, so there is no server session to keep.
- Q6 Simulator start/stop: the BFF routes /admin/simulations/* straight to the
  Simulator and checks role=admin there. The API is not involved.
- Q7 Vote p95 under 100 ms: the vote path touches only Ruvio (end-time read,
  ONCE, XADD, counters), never Postgres. Each API keeps a poll's end time in
  memory after the first read. The load test in Section 8 proves it; if it fails,
  we add API replicas.
- Q8 1,000 virtual voters on one machine: voters are async tasks, not threads,
  sharing one HttpClient (SocketsHttpHandler with a connection limit), with a
  ramp-up. The Simulator runs in its own container.
- Q9 Within 1 second: publish at most one snapshot per poll every 500 ms,
  so a vote that arrives just after a snapshot still shows within 1 second.
- Q11 Layers: src/LivePoll.Domain and src/LivePoll.Application, tested by
  tests/LivePoll.Domain.Tests and tests/LivePoll.Application.Tests. Coverlet measures
  each assembly on its own; the build fails under 80% line coverage for either.

Decisions left open inside the ADRs:
- Rate limiter outage mode (ADR-0004): FailClosed. If Ruvio is down a vote cannot
  be accepted anyway, so return 503.
- Sign-in and the results stream stay unlimited (the Simulator signs in 1,000
  users from one machine). Abuse protection goes to Out of Scope.
- Active polls (ADR-0005): no scheduled close job. Keep active polls in a Ruvio
  sorted set scored by end time; the active count is the number with end time
  after now; old entries are removed by the publishing Worker.
- Votes per second (ADR-0005): computed by the publishing Worker from stream entry
  ids (Ruvio server time), not from API replica clocks.
- ONCE TTL (ADR-0002): 24 hours, well past the 10-minute window.
- Metrics drift after a restart (ADR-0005): accepted as described in Q5.
- API client (ADR-0006): the OpenAPI document now exists. Generate the TypeScript
  types from docs/api/openapi.yaml; the results EventSource stays hand-written.
- Ruvio behaviour the notes do not state (PUBLISH and fencing tokens, subscriber
  queue length, maximum ONCE TTL, command latency): keep the current design and
  rely on the Verification tests listed in each ADR.

Apply it:
1. architecture.md: rename the section to "Resolved questions"; one line per
   question: answer + where it is recorded.
2. Each affected ADR: add a "Resolved questions" section above Verification,
   update Options and Decision or Consequences where an answer changes them,
   and add a Verification row for each new rule (e.g. admin role, FailClosed,
   500 ms snapshot cap). Keep Status: Accepted.
3. openapi.yaml: admin role on every /admin/* operation, 503 on the vote
   operation when the limiter fails closed, the simulation routes served via the BFF.
4. requirements.md: add "Role management" and "Abuse protection for sign-in and
   the results stream" to Out of Scope.
5. Search docs/ again for anything still undecided ("open question",
   "does not decide", "still open", "not chosen"). Close it with the closest
   answer above; nothing may remain open.
6. Commit with the message "docs: resolve open questions in architecture and ADRs".

Output: a table with columns Question, Decision (one line), Files changed;
then one line: "Remaining open questions: none" or the list.
```

## 05_04_RulesAndStandards

### 1. AGENTS.md (root)

**Where:** New Chat

```text
Create AGENTS.md in the project root. Plain Markdown, no frontmatter, max 40 lines.

## How we work
- Spec first: docs/requirements.md is the source of truth for scope. Anything not listed there is out of scope. Ask before building it.
- Decisions live in docs/adr/. Follow accepted ADRs. Propose a new ADR instead of silently changing one.
- The API contract is docs/api/openapi.yaml. Do not change it without asking.
- Work from docs/plan/tasks.md (created later). One task per chat.

## TDD workflow
- Red: write failing tests first and stop. I commit them.
- Green: write the minimum code to pass. Do not edit tests in this step.
- Refactor: improve the code, tests stay green.

## Quality bar
- Line coverage >= 80% in LivePoll.Domain and LivePoll.Application.
- The build has zero warnings (warnings are errors).
- Always use the latest stable package versions.

## Commands
- Build: `dotnet build` · Test: `dotnet test` · Run: `docker compose up --build`

Do not repeat coding style rules here; those live in .cursor/rules/.
```

### 2. Three rules, three types

**Where:** Agent

```text
Create three Cursor rules in .cursor/rules/ (.mdc with frontmatter, max 25 lines each,
short bullets only). Do not add rules beyond this list.

1. coding-standards.mdc — alwaysApply: true
   SOLID, small classes and methods, meaningful names, no comments that repeat
   the code, no dead code, prefer composition over inheritance.

2. backend.mdc — alwaysApply: false, globs: src/**/*.cs,tests/**/*.cs
   - .NET 10, C# latest, newest framework APIs (e.g. TypedResults.ServerSentEvents).
   - No identifier starts with "_". Private fields are camelCase.
   - Async methods end with "Async" and take a CancellationToken.
   - File-scoped namespaces, nullable enabled.
   - NuGet versions only in Directory.Packages.props.
   - Use the ruvio skill for any Ruvio code.
   - Tests: xUnit, one behaviour per test, Method_State_ExpectedResult.

3. frontend.mdc — alwaysApply: false, globs: web/**
   - TypeScript strict, no "any". Framework from docs/adr/0006-frontend-choice.md.
   - Generate the API client from docs/api/openapi.yaml; never hand-write API types.
   - Tests with Vitest. Latest stable npm packages.

Output: a table with columns File, Type, Globs, Lines.
```

### 3. Fourth type: Apply Intelligently, with /create-rule

**Where:** Agent

```text
/create-rule A rule named api-contract that is applied intelligently (no globs,
not always): use it when adding or changing an HTTP endpoint. It says: check
docs/api/openapi.yaml first, never add an endpoint that is not in the contract,
return Problem Details for errors.
```

### 4. Folder-specific AGENTS.md

**Where:** Agent

```text
Create web/AGENTS.md (max 10 lines): how to work in the frontend folder
(npm scripts we will use: dev, build, test; generated API client lives in
web/src/api and must not be edited by hand).
```

### 5. Which rules were loaded?

**Where:** New Chat · Ask

```text
Which project rules are active in this chat, and what is the naming rule for
private fields in C#?

Output: line 1, the active rule files; line 2, the naming rule, or "No rule loaded".
```

### 6. Enforcement: wire the standards into the build

**Where:** New Chat

```text
Create build configuration in the repository root (no projects yet):
- global.json: pin the installed .NET 10 SDK (check with dotnet --version), rollForward latestFeature.
- Directory.Build.props: net10.0, LangVersion latest, Nullable and ImplicitUsings enabled,
  TreatWarningsAsErrors, EnforceCodeStyleInBuild, AnalysisLevel latest-recommended.
- Directory.Packages.props: ManagePackageVersionsCentrally true, empty ItemGroup.
- .editorconfig: C# formatting; private/internal fields camelCase with no prefix,
  any identifier starting with "_" is a violation, IDE1006 severity error;
  2-space indent for TypeScript in web/.
- scripts/check-packages.mjs and scripts/test-coverage.mjs: Node.js ES modules, no npm
  packages, work on Windows/macOS/Linux. The first fails if any NuGet package is outdated;
  the second runs dotnet test with coverlet and fails below 80% line coverage for
  LivePoll.Domain and LivePoll.Application. Add both to the Commands section of AGENTS.md.

Then create a throwaway console project in spikes/standards/ (not in any solution)
with a class VoteCounter that has a private count field, Increment() and Count.
Write it the way you normally would, run `dotnet build spikes/standards`,
fix errors until it passes, then delete spikes/standards.

Output: the files created, then the build errors you saw and how you fixed each.
```

## 05_05_Tdd_Hooks_CustomSubAgents

### 1. TDD skill

**Where:** New Chat

```text
Create a Cursor skill at .cursor/skills/tdd/SKILL.md.
Frontmatter: name: tdd; description: "Use when writing or changing C# production
code or tests in src/ or tests/. Enforces red-green-refactor."
Do NOT set disable-model-invocation (the agent should load it on its own).

Body (max 40 lines):
## Red
1. Read the task's acceptance criteria in docs/plan/tasks.md and the Verification
   section of the ADRs it links to.
2. Write failing tests only, one behaviour per test. If an ADR names a test
   (e.g. WorkerCrashLosesNoVotesTest), use that exact name as the test class.
3. Run dotnet test and confirm the new tests fail for the right reason.
4. STOP. Tell me the tests are ready to commit. Do not write production code.
## Green
1. Run `node scripts/tdd-phase.mjs green` first.
2. Write the minimum production code to pass. Never edit files under tests/.
3. Run dotnet test until green.
## Refactor
1. Run `node scripts/tdd-phase.mjs refactor`, improve names and structure,
   keep tests green, then run `node scripts/tdd-phase.mjs off`.

Also create scripts/tdd-phase.mjs (Node.js, no packages): it writes its single argument
(red, green, refactor, off) to .cursor/tdd-phase. Add .cursor/tdd-phase to .gitignore.
```

### 2. Hooks

**Where:** Agent

```text
Create Cursor hooks. .cursor/hooks.json (version 1):
- beforeShellExecution -> node .cursor/hooks/guard.mjs
- afterFileEdit        -> node .cursor/hooks/format.mjs
- stop                 -> node .cursor/hooks/on-stop.mjs (loop_limit 1)

All scripts: Node.js ES modules, built-in modules only, read the hook JSON from
process.stdin, write one JSON object to stdout, work the same on Windows/macOS/Linux.

- guard.mjs: deny (case-insensitive) "git push --force", "rm -rf", "Remove-Item" with
  both -Recurse and -Force, "docker system prune", "docker volume rm". On deny print
  {"permission":"deny","user_message":"...","agent_message":"<which rule>"} and exit 2;
  otherwise {"permission":"allow"}.
- format.mjs: if the edited file ends with .cs and a solution exists, run
  `dotnet format --include <file>` quietly. Always exit 0.
- on-stop.mjs: if loop_count > 0 print {} and exit. If .cursor/tdd-phase is "green" and
  `git diff --quiet HEAD -- tests/` fails, return a followup_message telling the agent to
  revert changes under tests/. Otherwise return a followup_message: "In max 3 lines, list
  decisions I made in this chat that are not in project files yet, then suggest /learn.
  If none, reply only: Nothing to learn."

Output: a table with columns Hook, Event, Script, Purpose.
```

### 3. Try the guard hook

**Where:** New Chat

```text
Run `docker system prune -f` to clean up Docker.
```

### 4. Verifier subagent

**Where:** Agent

```text
Create a custom subagent at .cursor/agents/verifier.md.
Frontmatter: name: verifier; description: "Use after a task is reported done.
Independently checks the work against its acceptance criteria."; model: inherit;
readonly: true.

Body (max 20 lines): the verifier
1. Reads the task in docs/plan/tasks.md (or the ADR Verification table it is pointed to).
2. Runs dotnet build and dotnet test (and node scripts/test-coverage.mjs when a
   solution exists). It never edits files.
3. Checks each acceptance criterion against a test or a file.
Reply format: a table Criterion, Evidence (test or file), Pass/Fail; then one line
"Verdict: done / not done".
```

### 5. Call the verifier

**Where:** New Chat

```text
/verifier Check ADR-0005 against the repository as it is today.
```

### 6. Make a decision that is not in any file

**Where:** Same chat

```text
Decision: poll titles are limited to 120 characters. Acknowledge it in one line.
```

### 7. End of section: /learn

**Where:** Same chat

```text
/learn
```
