---
name: adr
description: Write an architecture decision record from docs/adr/0000-template.md. Use when the user invokes /adr or asks for an ADR.
disable-model-invocation: true
---

# ADR

1. Use docs/adr/0000-template.md and the number it is given (or the next free one).
2. Take "Context and Requests" and "Out of Scope" from my message, word for word. If they are missing, ask me. Never write or change human sections.
3. Read docs/requirements.md, docs/architecture.md, earlier ADRs and any relevant skill (for example the ruvio skill) before proposing.
4. Propose at most 3 options in a table, then one decision with reasons.
5. Map every request to at least one test in Verification.
6. Set Status: Proposed. Only set Accepted when I say "accept".
7. Reply with: file path, the decision in one line, open questions (max 3).
