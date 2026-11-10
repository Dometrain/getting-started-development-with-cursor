# Section 07 - Backend and Parallel Development: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names.

## 07_02_Wave1-3

### 1. Sync the plan with GitHub

**Where:** New Chat

```text
Tasks ticked [x] in @docs/plan/tasks.md still have open GitHub issues.
Close them with the GitHub MCP tools, naming the finishing commit in a comment.
Then list the open wave-1 and wave-2 issues.
```

### 2. Wave 1-2: five tasks in the cloud

**Where:** New Chat · Multitask

```text
Run these tasks from @docs/plan/tasks.md in parallel, one cloud subagent each, all in the cloud:
"Live results interval is one second", "Frontend shell and sign-in",
"Frontend poll pages and admin edits", "Simulator vote plan", "BFF routes and admin gate".
"Frontend poll pages and admin edits" starts from the "Frontend shell and sign-in" branch.
```

### 3. Close the merged tasks

**Where:** New Chat

```text
Close the merged tasks' issues with the GitHub MCP tools, tick them in @docs/plan/tasks.md,
commit "docs: close wave 1 and wave 2 tasks" and push.
```

### 4. Wave 3 in the cloud

**Where:** New Chat · Multitask

```text
Run the open wave-3 tasks from @docs/plan/tasks.md in parallel, one cloud subagent each,
all in the cloud: "Application poll lifecycle", "Frontend vote page and live chart",
"Simulator runs virtual voters".
```

### 5. Verify the last wave's branches

**Where:** New Chat

```text
/verifier Check the branches from the last wave (@Chats)
against their GitHub issues. Say READY or NOT READY for each.
```

### 6. Merge the READY branches

**Where:** Same chat

```text
Merge the READY branches into master on this computer, one at a time, running the tests
after each.
```

### 7. Run the whole system and the tests (optional manual check)

**Where:** Terminal

_Source: course script. It is not in the recording notes._

```powershell
docker compose up -d --build; dotnet test
```

## 07_03_Wave4-6

### 1. Verify and merge wave 3

**Where:** New Chat

```text
/verifier Check the wave-3 branches against their GitHub issues. Merge the READY ones
into master here one at a time, close their issues with the GitHub MCP tools and push.
```

### 2. Worktree setup

**Where:** Agent

```text
Create .cursor/worktrees.json so every new worktree runs dotnet restore and
npm ci --prefix web. Commit "chore: add worktree setup" and push.
```

### 3. Wave 4: one cloud, one local

**Where:** New Chat · Multitask

```text
Run the open wave-4 tasks from @docs/plan/tasks.md in parallel:
"Application vote acceptance and business metrics" in the cloud,
"Frontend metrics panel and simulator controls" on this computer in its own worktree.
```

### 4. See the panel in the browser

**Where:** Same chat

```text
Start the Vite dev server in this worktree, open the poll page in the built-in browser,
open the metrics panel and check the network and console.
```

### 5. Waves 5 and 6

**Where:** New Chat · Multitask

```text
Verify and merge wave 4 like the last wave. Then run the open wave-5 and wave-6 tasks
in parallel: "Coverage gate and TDD commit order" in the cloud,
"Ruvio queue, markers, and counters" on this computer in its own worktree.
("Postgres polls and votes" was finished in 6.3.)
```

## 07_04_Wave7-9_AND_TroubleShooting

### 1. Merge the worktree branch of waves 5-6

**Where:** Same chat

```text
Merge this worktree's branch into master on This PC and run the tests.
```

### 2. Verify and merge the cloud branch

**Where:** New Chat

```text
/verifier Check "Ruvio queue, markers, and counters" and the cloud branch of
"Coverage gate and TDD commit order" against their GitHub issues. Merge the READY work,
close the issues with the GitHub MCP tools and push.
```

### 3. Waves 7 and 8

**Where:** New Chat · Multitask

```text
Run these tasks from @docs/plan/tasks.md in parallel:
"API sign-in and poll routes" and "Worker stores each vote once" in the cloud,
"API vote route and rate limit" on this computer in its own worktree.
Start "API vote route and rate limit" after "API sign-in and poll routes" is pushed.
```

### 4. Best-of-N: the same task with two models

**Where:** New Chat · Best-of-N

```text
Do the task "Worker publishes one snapshot per second" from @docs/plan/tasks.md.
```

### 5. Compare the two results

**Where:** New Chat

```text
/verifier Check both worktree branches of "Worker publishes one snapshot per second"
against its GitHub issue and tell me which one you would keep and why.
```

### 6. Option A: merge the winning worktree

**Where:** Same chat

```text
Merge this worktree's branch into master on This PC and run the tests.
```

### 7. Option B: drop the losing worktree

**Where:** Same chat

```text
Delete this worktree and its branch.
```

### 8. Verify waves 7-8 and run wave 9

**Where:** New Chat · Multitask

```text
Verify and merge the wave-7 and wave-8 work like before. Then run the wave-9 tasks:
"API live results and metrics routes" in the cloud, and after it is merged,
"Vote flow against real Postgres and Ruvio" on this computer in its own worktree.
When both are merged and their issues are closed, the last commit message is
"test: add vote flow test against real Postgres and Ruvio". Push.
```

### 9. Start the whole system

**Where:** Terminal

```powershell
docker compose up -d --build
```

### 10. Create a poll and get the stream command

**Where:** New Chat

```text
Create a new poll through the API with three options and give me the curl.exe -N
command for its live results stream. Do not open the stream yourself and do not vote yet.
```

### 11. Cast three votes

**Where:** Same chat

```text
Now cast three votes on that poll from the terminal with different dev tokens,
one every 3 seconds. If a vote fails, show me the response and stop.
```

### 12. Fix 1: the vote route returns 404

**Where:** Same chat

_Source: course script. It is not in the recording notes._

```text
Votes return 404 "No poll exists with this id.". The vote route reads the poll's end time
from Ruvio, but creating a poll never calls RuvioPollEndTimes.SaveEndsAtAsync or
RuvioActivePolls.AddAsync; only tests/LivePoll.Integration.Voting/VotingStack.cs does.
Make creating a poll write both, make the test create the poll through the app,
and run the tests on This PC.
```

### 13. Fix 2: polls never reach Postgres

**Where:** Same chat

_Source: course script. It is not in the recording notes._

```text
The API registers InMemoryPollRepository, so polls never reach Postgres and the worker's
vote insert fails. Implement IPollRepository with Postgres in LivePoll.Infrastructure,
remove the in-memory one, and run the tests on This PC.
```
