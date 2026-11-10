# Section 02 - Requirements: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names. They are taken as-is from the course script.

## 02_02_SDD_Repo

### 1. Create the repository folder

**Where:** Terminal

```powershell
New-Item -ItemType Directory -Force "$HOME\src\livepoll" | Out-Null
Set-Location "$HOME\src\livepoll"
git init
```

### 2. The risk of "just build it"

**Where:** New Chat · Ask

```text
I want to build a live poll web app. Users create a poll, share a link,
people vote once, and results update live.

If I asked you to build this right now with no other information,
which decisions would you have to make on your own?
Do not write code.

Output: a table with columns Decision, What you would probably choose (max 10 rows), then one line: the biggest risk of letting you decide all of these.
```

### 3. Repository skeleton

**Where:** Agent (Build)

```text
This folder is a new Git repository for a project called LivePoll. Create:
- README.md: a title and one paragraph: a live poll app built spec-first with Cursor.
- .gitignore for .NET, Node.js, IDE files and .env files.
- .cursorignore that hides .env files and a secrets/ folder from the AI.
- docs/requirements.md with only these empty headings:
  "Functional Requirements", "Non-Functional Requirements", "Out of Scope".
Commit with the message "chore: initial repository structure".
Do not create any other files.
When done, reply only with the files created and the commit hash.
```

### 4. A question to see the default answer style

**Where:** Ask

```text
What is a spec-driven workflow, and why does it help when working with an AI agent?
```

### 5. First rule: response style

**Where:** Agent (Build)

```text
Create .cursor/rules/response-style.mdc with exactly this content:

---
description: Default response style for every answer
alwaysApply: true
---

# Response style

- Be concise. No preamble, no closing summary, no restating my request.
- Prefer numbered lists or tables. Max 10 items, one line each, unless I ask otherwise.
- Do not include code in answers unless I ask for code.
- After editing files, reply only with: the files changed and a one-line summary.
- If my request is ambiguous or needs a decision only I can make, do not guess.
  Ask at most 3 questions. Give each question 2-4 lettered options,
  with your recommended option first and marked "(recommended)".
- If my prompt contains an "Output:" line, it overrides these defaults.

Commit with the message "chore: add response style rule".
```

### 6. Second rule: where work runs

**Where:** Agent (Build)

```text
Create .cursor/rules/execution.mdc with exactly this content:

---
description: Where work runs - this computer or the cloud
alwaysApply: true
---

# Where work runs

Choose where each part of a task runs. Before you start, say in one line
what runs where and why.

- On this computer: anything that needs the running system - docker compose,
  the UI, browser checks, end-to-end and load tests, anything that calls localhost.
- In the cloud (a cloud subagent): self-contained work that needs no running
  stack - writing code, writing and running unit tests, refactoring, docs.
- If the cloud is not available, run everything on this computer.
- Cloud work comes back as a branch. Merge it here and run the full checks on
  this computer before the task counts as done.
- Parallel work on this computer uses git worktrees, one per subagent. Each
  subagent touches only the folders listed for its task in docs/plan/tasks.md.
  Never start docker compose in a worktree; run unit tests and Testcontainers tests only.
- Merge nothing until I have reviewed it.

Commit with the message "chore: add execution rule".
```

### 7. Connect to GitHub

**Where:** Terminal

```powershell
gh repo create livepoll --private --source . --push
```

### 8. Check the repository

**Where:** New Chat · Ask

```text
Check this repository for: README.md, .gitignore, .cursorignore,
docs/requirements.md with three headings, .cursor/rules/response-style.mdc,
.cursor/rules/execution.mdc, three commits, and an origin remote on GitHub.

Output: one line per item: OK or MISSING.
```

## 02_03_FunctionalRequirements

### 1. Let the AI interview you

**Where:** New Chat · Ask

```text
We are writing functional requirements for LivePoll, a live poll web app:
users create a poll, share a link, people vote, and results update live.

Before writing anything, interview me as a product analyst.
Focus only on what users can do, not on speed or scale.

Output: at most 8 numbered questions, one line each. Wait for my answers.
```

### 2. Your answers

**Where:** Same chat

```text
1. Any signed-in user can create a poll.
2. A poll has a title and 2 to 6 options.
3. A poll has an end time; after that it accepts no votes.
4. A poll is shared with a link. Anyone with the link can vote.
5. Each user can vote once per poll.
6. Everyone viewing the poll page sees results update live.
7. Identity is a simple dev identity: pick a username, get a token. No passwords.
8. There is an /admin page to run a simulation with virtual users,
   and a metrics side panel on the poll page.
```

### 3. Add what the AI did not ask about

**Where:** Same chat

```text
Extra context you did not ask about:
- /admin sets: number of virtual users, ramp-up time, duration, vote
  distribution per option. Start and stop buttons.
- The metrics panel shows votes per second, total votes, active polls,
  votes per option and the worker backlog.
- Virtual users vote through the same public API as real users.

Summarize all my answers as functional requirements.

Output: a numbered list with IDs FR-1, FR-2, ..., max 14 items, each one testable sentence.
```

### 4. Write the final list to the file

**Where:** New Chat (attach the previous chat with @)

```text
Write the final FR list from the attached chat into the "Functional Requirements"
section of @docs/requirements.md.
- Keep the FR-n IDs.
- Group them under: Polls, Voting, Live Results, Identity, Simulation, Business Metrics.
- Do not add requirements that are not in the list. Do not touch other sections.
```

## 02_04_NonFunctionalRequirements

### 1. Have the vague draft criticized

**Where:** New Chat · Ask

```text
Here is my first draft of non-functional requirements for LivePoll
(see @docs/requirements.md for the functional ones):

- The system should be fast.
- It should handle a lot of users.
- Results should be real-time.
- No votes should be lost.
- It should be scalable.
- It should be easy to run.
- The code should be well tested.

Act as a strict reviewer.

Output: a table with columns Draft line, Problem, Question I must answer; one row per draft line.
```

### 2. Give the answers

**Where:** Same chat

```text
My answers:
- Load: 1,000 concurrent virtual voters on one machine.
- Vote API latency: p95 under 100 ms at that load.
- Live results: a vote shows on a viewer's screen within 1 second.
- No vote is lost, even if a worker crashes.
- No double votes, even if the same request arrives twice.
- Votes are stored in Postgres within 5 seconds.
- API and Worker are stateless, each with at least 2 replicas.
  If one replica stops, live results keep working.
- The whole system starts with one command: docker compose up.
- Line coverage of at least 80% in the Domain and Application layers.

Rewrite these as non-functional requirements.

Output: a table with columns ID, Requirement, How we verify; IDs NFR-1 to NFR-9 in this order.
```

### 3. Write the final table to the file

**Where:** New Chat (attach the previous chat with @)

```text
Write the final NFR table from the attached chat into the "Non-Functional
Requirements" section of @docs/requirements.md.
Above the table add: "Targets are measured on the instructor's machine.
Your numbers may differ; the measuring method is what matters."
Do not change the table content. Do not touch other sections.
```

### 4. Check consistency

**Where:** New Chat · Ask

```text
Check @docs/requirements.md for conflicts or overlaps between the functional
and non-functional requirements.

Output: max 5 numbered problems, or only "No conflicts found."
```

## 02_05_OutOfScopeAndNewSkillLearn

### 1. Find the blind spots

**Where:** New Chat · Ask

```text
Read @docs/requirements.md. Act as a skeptical product owner for a live poll app.

Find what an implementer might build that we did not ask for, places where they
must guess, and questions only the product owner can answer.

Output: three headed lists: Likely scope creep, Ambiguities, Missing decisions. Max 6 items each.
```

### 2. Write the Out of Scope section

**Where:** Same chat · Agent (Build)

```text
Add an "Out of Scope" section to @docs/requirements.md with exactly these items,
each with a one-line reason:

- Changing a vote after it is cast
- Anonymous voting
- Multiple languages
- Email or push notifications
- Exporting results
- User management screens
- Real sign-up and login (passwords, OAuth). A simple dev identity is enough.
- Mobile apps
- Technical metrics and telemetry (business metrics stay in scope)
- A management screen for failed messages
- WebSocket or SignalR
- RabbitMQ or any other message broker
- Multi-region deployment

Put this line at the top of the section:
"Anything not listed in this document is out of scope. Ask before building it."
Do not add, remove or reword any items.
```

### 3. The /learn skill and project context

**Where:** Agent (Build)

```text
Create .cursor/skills/learn/SKILL.md (name: learn, one-line description,
disable-model-invocation: true). The skill tells the agent to:
1. List the questions it asked me in this chat and the decisions I made.
2. Skip anything already in the project files.
3. Pick a target for each: always-true rule -> .cursor/rules/*.mdc,
   task procedure -> .cursor/skills/<name>/SKILL.md, decision -> docs/adr/,
   requirement or out of scope -> docs/requirements.md.
4. Show a numbered proposal: item -> file -> exact text.
5. Write only what I approve. Keep entries short; merge, do not duplicate.

Also create .cursor/rules/project-context.mdc (alwaysApply: true): one paragraph
about LivePoll, and "docs/requirements.md is the source of truth for scope."
```

### 4. Try the loop

**Where:** New Chat

```text
I want to start on poll creation. Before planning, ask me what you need to know
about how polls behave. Do not plan or write code yet.

Output: at most 5 numbered questions. Wait for my answers.
```

### 5. Proof that the AI learned

**Where:** New Chat · Ask

```text
Can poll options be edited after a poll starts? Can a user change their vote?

Output: for each question, Yes/No and one line of evidence (file + section).
```
