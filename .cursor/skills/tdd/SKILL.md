---
name: tdd
description: "Use when writing or changing C# production code or tests in src/ or tests/. Enforces red-green-refactor."
---

## Red
1. Read the task's acceptance criteria in docs/plan/tasks.md and the Verification
   section of the ADRs it links to.
2. Write failing tests only, one behaviour per test. If an ADR names a test
   (e.g. WorkerCrashLosesNoVotesTest), use that exact name as the test class.
3. Run dotnet test and confirm the new tests fail for the right reason.
4. For LivePoll.Domain and LivePoll.Application only: STOP. Tell me the tests are ready to commit. Do not write production code.
## Green
1. Run `node scripts/tdd-phase.mjs green` first.
2. Write the minimum production code to pass. Never edit files under tests/.
3. Run dotnet test until green.
## Refactor
1. Run `node scripts/tdd-phase.mjs refactor`, improve names and structure,
   keep tests green, then run `node scripts/tdd-phase.mjs off`.
