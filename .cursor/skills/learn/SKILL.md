---
name: learn
description: Record approved chat decisions into rules, skills, ADRs, or requirements. Use when the user invokes the learn skill.
disable-model-invocation: true
---

# Learn

1. List the questions it asked me in this chat and the decisions I made.
2. Skip anything already in the project files.
3. Pick a target for each: always-true rule -> .cursor/rules/*.mdc, task procedure -> .cursor/skills/<name>/SKILL.md, decision -> docs/adr/, requirement or out of scope -> docs/requirements.md.
4. Show a numbered proposal: item -> file -> exact text.
5. Write only what I approve. Keep entries short; merge, do not duplicate.
