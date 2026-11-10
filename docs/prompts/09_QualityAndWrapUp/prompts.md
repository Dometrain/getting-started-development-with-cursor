# Section 09 - Quality and Wrap-up: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names. They are taken as-is from the course script.

## 09_02_DebugMode

### 1. Code with a planted bug (paste into the poll page's data mapping)

**Where:** File

```ts
const names = snapshot.options.map((option) => option.text).sort();
return {
  votesPerSecond: snapshot.votesPerSecond,
  bars: snapshot.options.map((option, index) => ({
    option: names[index],
    votes: option.votes,
  })),
};
```

### 2. Describe the bug to Debug mode

**Where:** New Chat · Debug

```text
On the poll page, a vote for one option raises another option's bar.
It happens when the options are not in alphabetical order.
```

## 09_03_LocalReview

### 1. A small change on a new branch

**Where:** New Chat

```text
On a new branch fix/simulation-limits: POST /admin/simulations has no upper limit.
Allow at most 2,000 voters and 600 seconds of duration; anything above returns 400.
```

### 2. Local review

**Where:** Same chat

```text
/review
```

### 3. Fix one finding

**Where:** Same chat

```text
Fix finding <n> only.
```

### 4. Pull request

**Where:** Same chat

```text
Commit, push and open a pull request to master with the GitHub MCP tools.
```

## 09_04_ScheduledAutomation

### 1. Automation prompt (set it up under Automations)

**Where:** Automations

```text
Check this repository for outdated packages: NuGet packages of every .NET project and npm
packages in web/. If any are outdated, open one GitHub issue titled "Outdated packages"
with a table: package, current version, latest version, project.
If an open issue with that title exists, update it instead. Do not change any code.
```

## 09_05_Restospective

### 1. Simplify the project instructions

**Where:** Same chat

```text
Review @AGENTS.md and every file in @.cursor/rules/ and @.cursor/skills/.
Remove duplicates, outdated facts and rules a tool already enforces (analyzers, tests, hooks).
Do not change code, tests or ADRs, and do not drop anything that is written nowhere else.
Show me the list of changes before you apply them.
```

### 2. Final check

**Where:** Same chat

```text
Can a user change their vote? Answer from the project files and name the file.
```
