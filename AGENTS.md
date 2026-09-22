# Project Instructions

## General
- Read this file before working on the project.
- Inspect existing code and project structure before making changes.
- Do not guess when the required information can be obtained from the project.
- Make the smallest change that correctly solves the requested task.
- Do not modify unrelated files.
- Preserve existing functionality unless the task explicitly requires changing it.
- Be concise. Communicate in Russian/Ukrainian with the owner (beginning vibe-coder) — with bullets and concrete run steps.
- Never claim that a change was tested, built, or completed unless it actually was.
- If a requirement is ambiguous and cannot be safely inferred from the project, ask before proceeding.

## Project
**STM — Sirko Time Manager** (TimerApp) — WPF, .NET 8 (`net8.0-windows`).
Author: Serhii Sirenko (Sirko). Version 0.1. Watch: `docs/PROJECT_MEMORY.md`.

Structure:
```
src\TimerApp\         — app (MainWindow.xaml + tabs; Controls/TimeDial; Models; Services; Localization uk/en/ru; Themes)
src\StmInstaller\     — self-extracting installer
make-installer.ps1    — installer build [-Mini] — DO NOT rebuild unless explicitly asked
build\                — STM-Setup*.exe outputs (not in git)
docs\PROJECT_MEMORY.md — long-term memory (see project-memory skill)
docs\ROADMAP.md        — owner's numbered backlog of planned features (update when adding/reworking ideas)
```
Data: `%AppData%\TimerApp\` (alarms.json, app_timers.json, settings.json, Sounds\); autostart registry `HKCU\...\Run\TimerApp` = `"exe" --autostart`.

Commands:
- Build app: `dotnet build -o bin\Test` (if locked — a TimerApp instance is running; stop it or ask owner).
- Smoke run: `.\bin\Test\TimerApp.exe`, then `Stop-Process -Name TimerApp -Force`.
- Installers rebuilt only when owner explicitly asks.

Project conventions:
- Every localization key must exist in all three `Localization\Strings.*.xaml` (uk/en/ru) — missing key = «набор символов».
- DynamicResource keys used in code-created WPF elements keep old brushes: theme change rebuilds lists (OnAppearanceChanged → Rebuild*).
- Options use switches (`SwitchStyle`), destructive confirmations use `ConfirmWindow` (dangerButton: true).
- Don't break the tray/full-close UX; don't change `settings.json` schema.

## PowerShell 5.1 traps (hard lessons)
- No ternary `? :`.
- NEVER use `Get-Content`/`Set-Content` without explicit UTF-8 for code/xaml/json — it reads ANSI and corrupts text (this already broke localization once). Use `[IO.File]::ReadAllText($f,[Text.Encoding]::UTF8)` / `WriteAllText` with `UTF8Encoding($false)`.
- No `dotnet build -t:Compile` for WPF — markup isn't compiled, produces fake errors (CS5001 etc.).

## Project Memory
Persistent project knowledge is stored in `docs/PROJECT_MEMORY.md`.
Use the `project-memory` skill when:
- starting a substantial task;
- changing architecture;
- investigating a problem with historical context;
- making or revisiting an important technical decision.
Keep project memory concise: decisions, constraints, failed approaches, important solutions, unresolved problems — not routine implementation details.

## Git
Use the `git-workflow` skill for Git operations.
- Check `git status`/`git diff` before significant changes; never overwrite or discard user changes.
- Do not create commits unless explicitly requested.
- Identity is configured: `Serhii Sirenko (Sirko)` / `Sirko-666@users.noreply.github.com`; don't touch `.gitignore` rules.
- Never attach installer exes to the repo — they go to GitHub Releases only.

## C#
Use the `csharp-workflow` skill for C#/.NET development.
Before changing code: identify the relevant project/files, understand the existing implementation, follow existing conventions.
Prefer minimal, targeted changes over unnecessary refactoring.
After implementation: build the affected project, smoke-run when reasonable, report results honestly.
