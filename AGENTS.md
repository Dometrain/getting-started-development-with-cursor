## How we work

- Spec first: docs/requirements.md is the source of truth for scope. Anything not listed there is out of scope. Ask before building it.
- Decisions live in docs/adr/. Follow accepted ADRs. Propose a new ADR instead of silently changing one.
- The API contract is docs/api/openapi.yaml. Do not change it without asking.
- Work from docs/plan/tasks.md. Find a task by its title. One task per chat.
  When a task is done, mark it [x] in tasks.md.

## Quality bar

- Always use the latest stable package versions.

## Commands

- Build: `dotnet build` · Test: `dotnet test` · Run: `docker compose up --build`
- Check packages: `node scripts/check-packages.mjs`
- Test coverage: `node scripts/test-coverage.mjs`

Coding style rules live in .cursor/rules/.
