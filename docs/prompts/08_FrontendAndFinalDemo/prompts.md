# Section 08 - Frontend and Final Demo: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names.

## 08_02_IntegrationGaps_RuvioUI

### 1. Add the Ruvio Operator UI

**Where:** New Chat

```text
Add the Ruvio Operator UI to docker-compose.yml for local testing, following
https://ruviodb.com/llms/ui-connections.txt . Put it under a "tools" profile so a plain
docker compose up does not start it; later only nginx may publish a port.
Publish it only on 127.0.0.1:8081. Fill the sign-in from the ruvio service and
RUVIO_REQUIREPASS. Start it on This PC.
```

### 2. Verify the fix by looking inside Ruvio

**Where:** Same chat

```text
Create a new poll and cast one vote on it from the terminal. Then in the built-in browser
open http://127.0.0.1:8081, sign in, and show me every Ruvio key for that poll.
Do not change any files.
```

### 3. Scan for remaining gaps

**Where:** New Chat · Ask

```text
Before waves 10 to 13 start, look for gaps between parts built separately:
- production code that registers in-memory or fake stand-ins for Postgres or Ruvio
- app state that only tests write and no production code writes
- settings a container needs that docker-compose.yml does not pass, and secrets in appsettings files
- errors that reach the HTTP response instead of the log
- files at the repo root that do not belong in the repo
- for each open task in @docs/plan/tasks.md, what it assumes already exists, and whether it does
List each gap with the file, why it breaks the running system, and which open task would hit it.
```

### 4. Close the gaps

**Where:** Same chat or New Chat

```text
Fix every gap above on This PC, with tests, except the missing nginx, BFF, frontend
and Simulator services: they belong to "Compose starts the whole system", so do not
build them now. Instead, update that task in @docs/plan/tasks.md and its GitHub issue
so its Touches and acceptance criteria cover them, the Simulator host, and API replica
names the BFF can reach.
A clean clone must start without a .env file.
```

### 5. Rebuild and start

**Where:** Terminal

```powershell
docker compose up -d --build
```

## 08_03_ComposeWholeSystem

### 1. What is open in wave 10?

**Where:** New Chat · Ask

```text
Using the GitHub MCP tools, list the open issues labeled wave-10.
```

### 2. Do the task

**Where:** Same chat

```text
Do the task "Compose starts the whole system" from @docs/plan/tasks.md.
When it is done, show me which containers publish a host port.
```

### 3. End-to-end with the built-in browser

**Where:** Same chat

```text
In the built-in browser, open http://localhost:8080 and use the app like a user.
Do not change any project files.
1. Sign in as "host" and create the poll "Best pizza?" with Margherita, Pepperoni, Veggie.
2. Open the poll link in a second tab and sign in as "alice"; vote Pepperoni.
3. Back in the first tab, wait until the chart updates and take a screenshot.
4. Read the console and the network requests of both tabs, and tell me every host
   the browser connected to.
```

### 4. Close the task

**Where:** Same chat

```text
Close the issue "Compose starts the whole system" with the GitHub MCP tools,
tick the task in @docs/plan/tasks.md, commit and push.
```

## 08_04_DesignMode_Polish

### 1. Base look

**Where:** Same chat (built-in browser open)

```text
The web app has no styling yet. Give it a clean base look with plain CSS:
one global stylesheet with CSS variables, a centered page container, readable
system fonts and spacing, styled buttons and inputs, and simple cards for the
poll, the metrics panel and the admin page. No new packages.
```

### 2. Commit

**Where:** Terminal

```powershell
git add -A
git commit -m "style: add base look with plain css"
```

### 3. Design Mode: a single note (annotate the title)

**Where:** Browser · Annotate

```text
Make this title larger and bold, and add the total vote count next to it in a muted color.
```

### 4. Design Mode: metric panel note

**Where:** Browser · Annotate

```text
Give each metric a small label above the number and align the numbers to the right.
```

### 5. Design Mode: admin page note

**Where:** Browser · Annotate

```text
Disable this button and show a short hint until the distribution adds up to 100.
```

### 6. Did the behavior survive?

**Where:** Changes

```text
Run the frontend tests, then in the browser vote once, read the chart, open and close
the Metrics drawer and read the console. Do not change any files.
```

### 7. Commit

**Where:** Terminal

```powershell
git add -A
git commit -m "style: polish poll page with design mode notes"
```

## 08_05_ReplicasAndTerminals

### 1. What is open in wave 11?

**Where:** New Chat · Ask

```text
Using the GitHub MCP tools, list the open issues labeled wave-11.
```

### 2. Terminal tab 1: start the system

**Where:** Terminal

```powershell
docker compose up -d
```

### 3. Terminal tab 2: API logs

**Where:** Terminal

```powershell
docker compose logs -f api1 api2
```

### 4. Terminal tab 3: worker logs

**Where:** Terminal

```powershell
docker compose logs -f worker1 worker2
```

### 5. Make the logs speak

**Where:** Same chat

```text
The worker logs nothing. Log with ILogger when this worker acquires or loses the
publisher lease (machine name and fencing token) and when a publish fails. Give the
worker services hostnames worker1 and worker2 in docker-compose.yml. Rebuild on This PC.
```

### 6. Do the task

**Where:** Same chat

```text
Do the task "Live results across replicas" from @docs/plan/tasks.md.
```

### 7. Live proof through nginx

**Where:** Same chat · built-in browser

```text
Through nginx on http://localhost:8080, on This PC:
1. Sign in as "host" and create the poll "Best pizza?" with Margherita, Pepperoni, Veggie.
2. Open its poll page in two built-in browser tabs.
3. Sign in as an admin and start a simulation on that poll: 100 users, 30 s ramp-up,
   120 s duration, 50/30/20 split, seed 42.
Give me the poll id, the option ids and the simulation id. Do not change any files.
```

### 8. Stop one API replica

**Where:** Terminal

```powershell
docker ps --format "table {{.Names}}\t{{.Status}}"
docker stop livepoll-api2-1
```

### 9. Stop the worker that holds the lease

**Where:** Terminal

```powershell
docker stop <lease-holder-worker-container>
```

### 10. Read the terminals

**Where:** Same chat

```text
Read @Terminals (the api and worker log tabs). Did the browser stream reconnect, who holds
the publisher lease now and when did the handover happen? Then compare vote counts for the
current poll in Ruvio and Postgres after the backlog is zero, and tell me how many votes
were lost. Do not change any files.
```

### 11. Start the stopped containers again

**Where:** Terminal

```powershell
docker start livepoll-api2-1 <lease-holder-worker-container>
```

### 12. Close the task

**Where:** Same chat

```text
Close the issue "Live results across replicas" with the GitHub MCP tools,
tick the task in @docs/plan/tasks.md, commit and push.
```

## 08_06_LoadCrashCanvas

### 1. What is open in waves 12 and 13?

**Where:** New Chat · Ask

```text
Using the GitHub MCP tools, list the open issues labeled wave-12 or wave-13.
```

### 2. Shrink the one-hour task and start its test in the background

**Where:** Same chat

```text
Before doing the task "Results stream stays open for one hour", change it in
@docs/plan/tasks.md and its GitHub issue: no test run may wait an hour.
- A fast test reads nginx.conf and the BFF settings: read and activity timeouts are
  at least one hour and buffering is off.
- ResultsStreamHoldsOneHour keeps a stream open through nginx and the BFF for
  RESULTS_HOLD_MINUTES (default 3) and checks that events keep arriving.
- RESULTS_HOLD_MINUTES=60 is a manual full run, not part of the normal test run.
Then do the task. Start the 3-minute test as a background shell and do not wait for it.
```

### 3. Crash recovery and one-machine load

**Where:** Same chat

```text
Do the task "Crash recovery and one-machine load" from @docs/plan/tasks.md.
Before the load test, print the CPU count, total memory and Docker's memory limit.
Have the load test write its settings and measurements (votes per second and backlog
each second, p95 vote latency, errors) to results/load-test-<UTC time>.json.
```

### 4. Crash test: stop both workers

**Where:** Terminal

_Source: course script. It is not in the recording notes._

```powershell
docker stop livepoll-worker1-1 livepoll-worker2-1
```

### 5. Crash test: start both workers again

**Where:** Terminal

_Source: course script. It is not in the recording notes._

```powershell
docker start livepoll-worker1-1 livepoll-worker2-1
```

### 6. Build the canvas dashboard

**Where:** Same chat

```text
Build a canvas called "LivePoll 1K run" from the newest file in results/
and the NFR table in @docs/requirements.md. For NFR-4, compare Ruvio and Postgres
counts yourself (read-only). Do not edit any project files.

The canvas must show:
- A header with the machine specs, simulator settings and the git commit hash.
- One card per NFR: target, measured value, Pass/Fail, evidence.
- A chart of votes per second and backlog over the run.
- p95 vote latency next to its target, and time-to-screen from the replica tests if measured.
- A short "Biggest risk" note, and whether the bottleneck is the system or the simulator.
```

### 7. Refine the canvas

**Where:** Same chat

```text
In the canvas, sort the NFR cards with failures first and add a filter "Only failed".
```

### 8. Save the results and close the tasks

**Where:** Same chat

```text
Save the NFR table from the canvas to docs/results/load-test-<today's date>.md with the same
header. Close the issues "Crash recovery and one-machine load" and "Results stream stays open
for one hour" with the GitHub MCP tools and tick both tasks in @docs/plan/tasks.md.
```

### 9. Commit and push

**Where:** Terminal

```powershell
git add -A
git commit -m "docs: record 1K load test results"
git push
```
