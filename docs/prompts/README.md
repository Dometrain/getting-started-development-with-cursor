# Course Prompts

Every prompt, command and configuration used in the course, grouped by section. Open a section's `prompts.md` and follow it from top to bottom; each `##` heading is the name of the video file where the prompts are used.

| Section | Prompts |
|---|---|
| 00 Course Introduction | [prompts.md](00_CourseIntroduction/prompts.md) |
| 01 Installations | [prompts.md](01_Installations/prompts.md) |
| 02 Requirements | [prompts.md](02_Requirements/prompts.md) |
| 03 System Design | [prompts.md](03_SystemDesign/prompts.md) |
| 04 Teaching Ruvio to AI | [prompts.md](04_TeachingRuvioToAI/prompts.md) |
| 05 Decisions and Standards | [prompts.md](05_DecisionsAndStandards/prompts.md) |
| 06 Plan, Infrastructure and Tools | [prompts.md](06_PlanInfraAndTools/prompts.md) |
| 07 Backend and Parallel Development | [prompts.md](07_BackendAndParallelDevelopment/prompts.md) |
| 08 Frontend and Final Demo | [prompts.md](08_FrontendAndFinalDemo/prompts.md) |
| 09 Quality and Wrap-up | [prompts.md](09_QualityAndWrapUp/prompts.md) |

## How to read an entry

- **Where** tells you the Cursor mode or place to use: *New Chat*, *Same chat*, *Ask*, *Plan*, *Multitask*, *Debug*, *Terminal*, the built-in browser, and so on.
- Fenced blocks marked `text` are prompts you type into the Cursor chat. Blocks marked `powershell` are terminal commands. Blocks marked `json` are configuration files.
- Entries marked _"Source: course script. It is not in the recording notes."_ are part of the course script but were not in the notes taken during recording. You may skip them.

## Placeholders and notes

- `@docs/...`, `@AGENTS.md`: type `@` in the composer and pick the file.
- `@Chats`, `@Terminals`: pick the chat or terminal tab from the `@` menu. If `@Chats` is not available, paste the branch names from the previous chat instead.
- `<lease-holder-worker-container>`: replace it with the worker container that holds the publisher lease, as shown in the worker logs.
- Wave tasks refer to task titles in `docs/plan/tasks.md`, which the course creates in section 06.
- In section 07 the verify step of one video can appear at the start of the next one. The prompts follow the order shown in the videos.
