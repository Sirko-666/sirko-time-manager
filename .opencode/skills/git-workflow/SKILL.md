---
name: git-workflow
description: Use Git safely while modifying the STM project; preserve user changes, inspect diffs, create meaningful commits, publish releases with installer attachments. Use when the owner says "закомить", "commit", "запушь", "git push", "обнови релиз", "GitHub release", "выложи", "сделай релиз".
---

# Git Workflow

## Before significant changes
1. Check `git status`.
2. Inspect relevant existing changes (`git diff`, `git log -3`).
3. Do not overwrite or discard user changes; if conflicts exist — surface them, don't force.
4. Make the smallest reasonable change; never mix unrelated changes into one commit.

## After significant changes
1. Build the affected project (see csharp-workflow).
2. Smoke-run when reasonable.
3. Inspect the final `git diff`.
4. Summarize what changed — never claim built/tested unless it happened.

## History
Use history when investigating why code exists, finding a previous implementation, or determining when behavior changed. Prefer `git log` → `git show` → `git diff` → `git blame`.

Do not `reset`, `checkout`, `clean`, or otherwise discard working changes unless explicitly requested.

## Commits
Create a commit only when the user asks for one, or the task explicitly requires it.
Message style: short, imperative, project language (Russian/Ukrainian), e.g. `v0.2 - кнопка видалення`, `fix: автозапуск скрыто в трей`.
Do not mix unrelated changes.

## Releases (GitHub)
1. Bump `<Version>` in both csproj (TimerApp + StmInstaller).
2. Run `powershell -File make-installer.ps1` (full) and `-Mini` when asked.
3. Releases → Draft: tag `vX.Y`, title `STM vX.Y — ...`, attach `build\STM-Setup.exe` (+ `-Mini`); description from README sections.
4. Installer exes are uploads-only — never placed into the repo (221 MB file; releases allow it, repo doesn't).
5. After a release, update `docs/PROJECT_MEMORY.md` Current State.
