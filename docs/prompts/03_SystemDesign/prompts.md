# Section 03 - System Design: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names. They are taken as-is from the course script.

## 03_02_HighLevelArchitecture

### 1. Ask for a plan

**Where:** New Chat · Plan

```text
Plan a high-level architecture document at docs/architecture.md.
Use @docs/requirements.md as the source of truth.

These tool choices are fixed (do not question them):
- Backend services in .NET 10
- PostgreSQL for durable storage
- Ruvio (a Redis-compatible server) for fast state: counters, the vote queue,
  live messaging between instances, locks, rate limiting and business metrics
- nginx in front, then a YARP backend-for-frontend (BFF)
- Docker Compose to run everything; no RabbitMQ or other broker

Components: nginx, BFF, API (N replicas), Worker (N replicas), Simulator,
Ruvio, Postgres, Frontend (framework not chosen yet).

The document must have:
1. A Mermaid component diagram.
2. A responsibilities table: Component, Does, Does not.
3. A short "Open questions for ADRs" list.

Describe behavior, not mechanisms. Do not name Ruvio commands, libraries or retry strategies.

Output: a plan of max 6 one-line steps plus the Mermaid diagram draft.
```

### 2. Edit the plan by hand

**Where:** Plan

```text
- In the responsibilities table, the Simulator row must say:
  Does not: access Ruvio or Postgres directly.
```

### 3. Build the plan

**Where:** Plan

```text
Build this plan.
```

### 4. Coverage check

**Where:** New Chat · Ask

```text
Compare @docs/architecture.md with @docs/requirements.md.

Output: the FR/NFR IDs no component is responsible for (max 5 lines), or only "All requirements are covered."
```

## 03_03_FlowsAndData

### 1. Plan the flows

**Where:** New Chat · Plan

```text
Plan an update to @docs/architecture.md. Use @docs/requirements.md as the source of truth.

Add these sections:
1. "Vote flow": a Mermaid sequence diagram from the user's click to the vote
   stored in Postgres, including the one-vote-per-user check.
2. "Live results flow": several Worker replicas exist but only one publishes a
   result snapshot about once per second. Every API replica receives it and
   forwards it to its own browsers over SSE. Usually one browser is watching.
3. "Simulation flow": from /admin to virtual users voting through nginx.
4. "Data placement": a table with columns Data, Stored in, Why.
5. "Business metrics": the metrics in the requirements and where they live.

Describe behavior, not mechanisms. Do not name Ruvio commands or libraries.
Do not change existing sections.

Output: a plan of max 6 one-line steps plus draft diagrams.
```

### 2. Build the plan

**Where:** Plan

```text
Build this plan.
```

### 3. Map the flows to the NFRs

**Where:** New Chat · Ask

```text
Read @docs/architecture.md and @docs/requirements.md.
For each NFR, say which flow or data choice supports it.

Output: a table with columns NFR, Supported by, Gap (if any); one row per NFR.
```

### 4. Record the gaps as open questions

**Where:** Agent (Build)

```text
Add the gaps from my review to the "Open questions for ADRs" list in
@docs/architecture.md. One line each. Do not change anything else.
```
