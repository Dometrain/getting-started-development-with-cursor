# Section 04 - Teaching Ruvio to AI: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names. They are taken as-is from the course script.

## 04_02_WhenAIDoesntNow

### 1. Knowledge test without any sources

**Where:** New Chat · Ask

```text
Answer from your own knowledge only. Do not search the web and do not read any files.

Ruvio is a database server used in this project. Answer these questions about it:
1. What is Ruvio and which protocol does it speak?
2. What is the official .NET client NuGet package, and how do you open a connection?
3. How do you limit a user to 10 requests per second using Ruvio commands?
4. How do you make sure only one worker runs a job at a time, with a fencing token?
5. How do you make sure the same request is processed only once?
6. How do you build a work queue with consumer groups, and how do you take over
   messages from a crashed consumer?
7. Can a browser connect to Ruvio directly to receive live updates?

Output: a numbered list matching the questions, max 2 lines each, each ending with a confidence tag [high], [medium] or [low]. No code.
```

### 2. Save the answers

**Where:** Same chat · Agent (Build)

```text
Save your previous answers, unchanged, to docs/ruvio-baseline.md
under the heading "## Before the Ruvio skill".
Add today's date and the model name at the top.
```

### 3. Spike: code written from guesses

**Where:** Agent (Build)

```text
Create a small .NET 10 console app in spikes/ruvio/ that:
1. Connects to Ruvio on localhost:6379.
2. Limits user "alice" to 10 requests per second.
3. Acquires a lease named "aggregator" with a fencing token, then releases it.

Use what you believe is the official Ruvio .NET client.
Do not search the web. Do not run the app. Do not start Docker.

Output: the NuGet packages you added and the Ruvio commands or client methods you used, one per line.
```

### 4. Make the AI question itself

**Where:** Ask

```text
Look at @spikes/ruvio/ again. Which parts are you unsure about because
you do not know Ruvio's real API?

Output: max 5 items, each as <file:line> — <what you guessed>. No fixes.
```

## 04_03_ConnectionSources

### 1. Source map from llms.txt

**Where:** New Chat · Ask

```text
We need Ruvio knowledge for this project.

Start from https://ruviodb.com/llms.txt and follow its reading rules.
Open only the pages we need. Do not open llms-full.txt.

Topics we need, based on @docs/architecture.md:
.NET client and connection, ASP.NET Core registration, rate limiting,
leases with fencing tokens, idempotency, streams with consumer groups,
pub/sub, counters, running Ruvio in Docker (auth and durability).

Output: a table with columns Topic, Page URL (from llms.txt), Size, Why this page.
```

### 2. Write local reference notes

**Where:** Same chat · Agent (Build)

```text
Using the pages in the table above, create one Markdown file per topic in
.cursor/skills/ruvio/references/ :

commands-index.md   (only the commands this project may use, one line each)
dotnet-client.md    (packages, connection, DI registration, typical calls)
rate-limiting.md
lease-and-locks.md
idempotency.md
streams.md
pubsub.md
docker.md           (image salihcantekin/ruvio:latest, port 6379, RUVIO_REQUIREPASS,
                     durability setting and data volume for this project)

Rules for every file:
- Only facts from those pages. If you are not sure, write "UNVERIFIED".
- Each fact ends with its source page URL.
- Prefer short code examples taken from the pages.
- Never copy Redis knowledge that the pages do not confirm.
- Max about 120 lines per file. No marketing text.
- Do not create SKILL.md yet.

Output: a table with columns File, Lines, Pages used, UNVERIFIED count.
```

### 3. Verify one item (done by you)

**Where:** Agent (Build)

```text
In @.cursor/skills/ruvio/references/lease-and-locks.md item "<UNVERIFIED item>"
is confirmed by https://ruviodb.com/llms/commands.txt . Remove the UNVERIFIED tag and cite that page.
```

## 04_04_WritingRuvioSkills

### 1. Design the skill skeleton

**Where:** New Chat · Ask

```text
We will write a Cursor skill at .cursor/skills/ruvio/SKILL.md.
The agent must load it automatically whenever a task involves Ruvio.

Read @.cursor/skills/ruvio/references/ and @docs/architecture.md.
Also read @docs/ruvio-baseline.md to see the mistakes made without this skill.

Propose:
1. The description field (max 2 sentences). It must say WHEN to use the skill.
2. The section headings of SKILL.md.
3. A "Do not" list based on the mistakes in the baseline.

Output: three numbered parts with the headings above; part 2 max 7 headings, part 3 max 8 items. No code.
```

### 2. Write SKILL.md

**Where:** Same chat · Agent (Build)

```text
Create .cursor/skills/ruvio/SKILL.md using the plan above.

Frontmatter:
- name: ruvio
- description: the one you proposed in part 1

Rules:
- Keep SKILL.md under 100 lines. It is a map, not a manual.
- "Which command for which problem" must be a table with columns:
  Problem, Ruvio feature, Reference file.
- Link every detail to a file in references/ instead of copying it.
- Only facts that exist in references/. Do not add new facts.
- Include the "Do not" list.

Output: the line count and the list of headings.
```

### 3. Does the skill load by itself?

**Where:** New Chat · Ask

```text
In our architecture, how should the worker make sure that only one replica
publishes poll results every second, even if a replica pauses for a long time?

Output: max 4 bullets naming the exact commands or packages, then one line: which skill or files you used.
```

### 4. Fix the description (only if needed)

**Where:** Agent (Build)

```text
The ruvio skill was not loaded for a question about "only one replica publishes".
Improve the description in @.cursor/skills/ruvio/SKILL.md so it also matches
tasks about single writers, background jobs and cross-instance messaging.
Keep it under 2 sentences.

Output: the old and the new description.
```

## 04_05_VerifyWhatAILearned

### 1. Same questions, now with the skill

**Where:** New Chat · Ask

```text
Answer using the project files if you need them. Do not search the web.

Ruvio is a database server used in this project. Answer these questions about it:
1. What is Ruvio and which protocol does it speak?
2. What is the official .NET client NuGet package, and how do you open a connection?
3. How do you limit a user to 10 requests per second using Ruvio commands?
4. How do you make sure only one worker runs a job at a time, with a fencing token?
5. How do you make sure the same request is processed only once?
6. How do you build a work queue with consumer groups, and how do you take over
   messages from a crashed consumer?
7. Can a browser connect to Ruvio directly to receive live updates?

Output: a numbered list matching the questions, max 2 lines each, each ending with a confidence tag [high], [medium] or [low] and its source file in parentheses. No code.
```

### 2. Before / after score

**Where:** Same chat · Agent (Build)

```text
Add your answers to @docs/ruvio-baseline.md under "## After the Ruvio skill".
Then add a "## Comparison" table with columns:
Question, Before (short), After (short), Correct? (Before / After).

Use these correct answers to fill the Correct? column:
1 Redis-compatible, RESP. 2 Ruvio.Client. 3 LIMIT. 4 LEASE/RENEW/RELEASE.
5 IDEM/ONCE. 6 Streams with XREADGROUP, XACK, XAUTOCLAIM. 7 No browser gateway.

Output: score before and score after (for example 2/7 → 7/7).
```

### 3. Run the spike against a real Ruvio

**Where:** Agent (Build)

```text
Rewrite the console app in @spikes/ruvio/ so it does the same three things:
1. Connects to Ruvio on localhost:6379.
2. Limits user "alice" to 10 requests per second and prints how many of
   15 quick requests were allowed.
3. Acquires a lease named "aggregator", prints the fencing token, tries to
   acquire it again (must fail), then releases it.

Then:
- Start Ruvio in Docker as described in @.cursor/skills/ruvio/references/docker.md
  with the container name ruvio-spike.
- Run the app and show me its output.

Output: packages used, the Docker command you ran, and the program output.
```

### 4. Cleanup

**Where:** Agent (Build)

```text
Stop and remove the ruvio-spike container, then delete the spikes/ folder.
Do not touch any other file.

Output: the commands you ran.
```
