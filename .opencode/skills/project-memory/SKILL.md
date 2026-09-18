---
name: project-memory
description: Manage the project's persistent memory in docs/PROJECT_MEMORY.md. Use when starting a substantial task, changing architecture, investigating a problem with historical context, making or revising important technical decisions, or when the owner says "запомни", "сохрани", "запиши в память", "обнови memory", "что мы уже сделали".
---

# Project Memory

Persistent project memory is stored in `docs/PROJECT_MEMORY.md`.

## Reading
Read the memory when:
- starting a substantial task;
- changing architecture;
- modifying an important system (installers, autostart, sounds, theme, select-mode);
- investigating a problem that may have historical context;
- about to reverse a documented decision — check it exists in memory first.

For small local edits (typos, single-label tweaks, indentation) do NOT load it.

## Updating
Update only when information is genuinely important for future work. Store:
- architectural decisions (+WHY);
- important constraints;
- successful solutions;
- failed approaches and why they failed;
- important dependencies;
- unresolved technical problems;
- decisions that should not be accidentally reversed.

Do NOT store:
- temporary conversation details; routine implementation steps;
- large code blocks; facts obvious from the source code;
- features list / user-facing description (that belongs to README.md).

Style: entries must be short, factual, one-two lines each; dated history events appended as `YYYY-MM-DD: essence`; keep the file under ~200 lines — prune superseded entries instead of growing forever.

## Rules
- Before changing an existing architectural decision, check whether it is documented in memory and whether the change invalidates the note — if so, update the note in the same task.
- Never invent historical information. If something is unknown, say so.
- Never claim something was decided/implemented unless memory or code proves it.
