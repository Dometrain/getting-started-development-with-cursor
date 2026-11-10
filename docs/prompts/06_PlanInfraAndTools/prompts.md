# Section 06 - Plan, Infrastructure and Tools: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names.

## 06_02_PlanModeAndModels

### 1. Ask for the plan

**Where:** New Chat · Plan · strong model

```text
Create the implementation plan for LivePoll as a new file docs/plan/tasks.md.
Read first: @docs/requirements.md @docs/architecture.md @docs/adr/ @docs/api/openapi.yaml @AGENTS.md
@.cursor/rules/execution.mdc

Rules:
- Each task fits one chat and one TDD cycle (or a few). Give each task a short,
  descriptive title (for example "Solution skeleton and Docker Compose").
- Only what is in docs/requirements.md. Nothing from "Out of Scope".
- No task may depend on an unresolved question. If you find one, ask me first.
- Every acceptance criterion is testable. Link NFRs by code (NFR-1 ... NFR-9).

Each task uses this template:
### <title>
- Depends on: <task titles or "none">
- Touches: <folders>
- Where: local | cloud-ok (cloud-ok only if it needs no running stack:
  no Docker, no browser, no localhost; see execution.mdc)
- Wave: <number> (tasks in the same wave can start together: their dependencies
  are done and they touch different folders)
- Acceptance criteria:
  - <testable statement>
- Definition of done: tests green, coverage passes where it applies,
  zero build warnings, one commit.

At the top add a "Waves" table: Wave, Tasks, Can run in parallel with.
Add a Mermaid flowchart of the dependencies.
Ask me at most 3 questions first.
```

### 2. Edit the plan in the Plans tab (add this line to the relevant task)

**Where:** Plans tab

```text
  - When one API replica stops, an open SSE client reconnects and keeps receiving results (NFR-7).
```

### 3. Build the plan

**Where:** Plan

_Source: course script. It is not in the recording notes._

```text
Build this plan.
```

### 4. Have another model review the plan

**Where:** New Chat · Ask · a different model

```text
Review @docs/plan/tasks.md against @docs/requirements.md, @docs/adr/ and
@.cursor/rules/execution.mdc.
Check: every FR and NFR-1...NFR-9 is covered; nothing from "Out of Scope";
dependencies are correct; tasks in the same wave do not touch the same folders;
"cloud-ok" tasks really need no Docker, browser or localhost;
every acceptance criterion is testable.

Output: a table Check, Result (OK / Problem), Detail; then max 5 problems, or "No problems".
```

### 5. Fix the problems

**Where:** Agent

```text
Fix the problems you found in @docs/plan/tasks.md. Only change the affected tasks.
For a file two tasks in the same wave must both change, add a "Shared file" note
that names the file and says: "Change only one registration line here."
```

### 6. Write the model class into the plan

**Where:** Agent

```text
Add a "Model" line to each task in @docs/plan/tasks.md:
"Strong" for concurrency, consistency or tricky Ruvio logic; "Fast" for scaffolding
and repetitive work; "Auto" for everything else. Do not change anything else.

Output: a table Task, Model, Reason (max 8 words).
```

### 7. Link AGENTS.md to the plan and commit

**Where:** Agent

```text
In @AGENTS.md, replace "Work from docs/plan/tasks.md (created later)." with:
"Work from docs/plan/tasks.md. Find a task by its title. One task per chat.
When a task is done, mark it [x] in tasks.md."
Commit everything with the message "docs: add implementation plan with lanes".
```

## 06_03_GithubMcp

### 1. Before: without the MCP

**Where:** New Chat · Ask

```text
Tell me about this project's GitHub repository:
1. Repository name and default branch.
2. Which labels exist.
3. Any open issues.

Output: the three answers, then one line: which tool you used (shell command or MCP tool) and its exact name.
```

### 2. After: with the GitHub MCP

**Where:** New Chat · Ask

```text
Use the GitHub MCP tools, not the gh CLI.
Tell me about this project's GitHub repository:
1. Repository name and default branch.
2. Which labels exist.
3. Any open issues.

Output: the three answers, then one line: which tool you used (shell command or MCP tool) and its exact name.
```

### 3. MCP server settings (Cursor MCP configuration)

**Where:** Settings

```json
{
  "mcpServers": {
    "github": {
      "url": "https://api.githubcopilot.com/mcp/",
      "headers": {
        "Authorization": "Bearer ${env:GITHUB_PAT}",
        "X-MCP-Features": "mcp_apps_disable_form_deferral"
      }
    }
  }
}
```

### 4. Set the GitHub token as an environment variable

**Where:** Terminal

```powershell
[Environment]::SetEnvironmentVariable("GITHUB_PAT", "your_github_pat", "User")
```

### 5. Turn the plan into GitHub issues

**Where:** New Chat

```text
Using the GitHub MCP tools (not the gh CLI), create one GitHub issue for each task in
@docs/plan/tasks.md.
- Title: the task title.
- Body: the task's acceptance criteria, definition of done, Where and Wave,
  and a link back to docs/plan/tasks.md.
- Labels: "wave-<n>" from the task's Wave, and "local" or "cloud-ok" from Where.
  Create missing labels first. Optional tasks also get "optional".
- Do not create anything else.
Then add an "Issue: #<number>" line under each task in docs/plan/tasks.md and commit it
with the message "docs: link plan tasks to GitHub issues".

Output: a table Task title, Issue number, Labels.
```

### 6. Persistent permission in a file

**Where:** New Chat

```text
Create .cursor/permissions.json with only this MCP allowlist:
read-only GitHub tools (list, get, search) may run without asking.
Creating, updating or closing anything on GitHub must still ask me.
Do not add terminal commands yet.
Commit it with the message "chore: add MCP allowlist".

Output: the file content.
```

## 06_04_SolutionAndPermissions

### 1. Solution and Docker Compose (Run Mode: ask for everything)

**Where:** New Chat · Auto model

```text
Find the task in @docs/plan/tasks.md that sets up the solution and Docker Compose, and do it.
Follow @AGENTS.md and the project rules. Use the ruvio skill for the Ruvio container.
Constraints:
- Only the projects that task needs. No Simulator or BFF yet.
- docker-compose.yml with a top-level `name: livepoll` and services named exactly
  postgres, ruvio, api, worker. Health checks for postgres and ruvio; api and worker
  start after both are healthy.
- Ruvio: image salihcantekin/ruvio:latest, port 6379 only on the internal network,
  RUVIO_REQUIREPASS from a .env file (not committed; add .env.example), durability
  mode from @docs/adr/0002-vote-processing.md with a named volume on /data.
- All package versions in Directory.Packages.props, latest stable.
When finished run `dotnet build`, then `docker compose up --build -d`, then call GET /health.

Output: a table Check, Result (build, compose services healthy, /health status), then the image tags used.
```

### 2. Move the permissions into a file

**Where:** New Chat

```text
Add a terminal allowlist to @.cursor/permissions.json so these commands run without asking:
dotnet build, dotnet test, dotnet restore, dotnet format, dotnet new, dotnet sln,
dotnet add, dotnet list, dotnet ef, docker compose, node scripts/
Keep the existing MCP allowlist. Nothing else.
```

### 3. Sandbox: blocked network request

**Where:** Agent

```text
Run a terminal command that downloads https://ruviodb.com/llms.txt and prints its first 5 lines.
If the sandbox blocks the request, tell me which domain was blocked and stop.
```

### 4. Sandbox: allow the domain

**Where:** Agent

```text
Create .cursor/sandbox.json that allows ruviodb.com in addition to the default domains.
Then run the same command again.
```

### 5. Check the services in the terminal tab

**Where:** Terminal

```powershell
docker compose ps
```

### 6. Read the terminal logs

**Where:** Terminal

_Source: course script. It is not in the recording notes._

```powershell
docker compose logs --tail=20 ruvio
```

### 7. Ask about the terminals with @Terminals

**Where:** New Chat · Ask

_Source: course script. It is not in the recording notes._

```text
@Terminals Are all four services healthy? Is anything in the Ruvio log a warning I should fix?
```

### 8. Commit

**Where:** Terminal

```powershell
git add -A; git commit -m "feat: add solution skeleton and docker compose"
```

### 9. Domain rules, schema and seed: Red phase

**Where:** New Chat · Auto model

```text
Find the task in @docs/plan/tasks.md that adds the domain rules, the database schema
and seed data, and do it. Follow the tdd skill: write the domain tests first,
show they fail, and stop.
```

### 10. Commit the tests

**Where:** Terminal

```powershell
git add tests/; git commit -m "test: add domain rule tests"
```

### 11. Green phase and persistence

**Where:** Same chat

```text
Green phase, then the persistence part of the same task. Requirements:
- EF Core with Npgsql; versions in Directory.Packages.props.
- Unique constraint on (poll id, user id) for votes.
- Development-only seed: 3 sample polls with 3-4 options each, no votes,
  applied when the Api starts in Development.
- A read-only database user "livepoll_readonly" in deploy/postgres/init.sql.
Run `docker compose up --build -d` and confirm the Api starts.
Mark both tasks as done in docs/plan/tasks.md and commit with the message
"feat: add domain rules, schema, migrations and seed data".
```

### 12. Queue a long job: rebuild from scratch

**Where:** Same chat or New Chat

_Source: course script. It is not in the recording notes._

```text
Rebuild everything from scratch to prove the setup works on a clean machine:
docker compose down -v, then docker compose build --no-cache, then docker compose up -d,
then dotnet test. Report each step as it finishes.
```

### 13. Queue a follow-up while it runs

**Where:** Same chat

_Source: course script. It is not in the recording notes._

```text
When you are done, also tell me how long the image build took.
```

### 14. Steering: a correction while the agent works

**Where:** Same chat

```text
Do not hard-code the Ruvio image anywhere in tests. Read it from docker-compose.yml
or from one shared constant.
```
