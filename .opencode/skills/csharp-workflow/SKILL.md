---
name: csharp-workflow
description: Safe and efficient workflow for developing and debugging the WPF .NET 8 solution (TimerApp + StmInstaller) on Windows PowerShell 5.1. Use when building, running, testing, debugging, collecting installers or touching localization/themes — keywords "собери", "dotnet build", "пересобери", "проверить", "локализация", "сборка", "smoke".
---

# C# Workflow

## Before modifying code
1. Identify the relevant project and files (`src\TimerApp`, `src\StmInstaller`).
2. Inspect the existing implementation; reuse existing patterns (ConfirmWindow, controllers, SettingsStore).
3. Avoid unnecessary refactoring; smallest effective change.

## Bug fixing order
1. Reproduce or identify the failure (build output, event log, smoke run).
2. Find the root cause.
3. Make the smallest effective change; build; verify; inspect the final diff.

## Adding functionality
Understand existing architecture → find the smallest integration point → implement only what is asked → build/test. Report build results honestly — never claim verified unless done.

## Do not
- rewrite working systems without reason;
- introduce dependencies without justification;
- change public shapes (`settings.json` schema, localization keys) casually;
- modify unrelated files; hide build errors.

## Build & verify
```powershell
dotnet build -o bin\Test      # solution-level; outputs land in repo-root bin\Test
.\bin\Test\TimerApp.exe       # smoke
Stop-Process -Name TimerApp -Force   # cleanup
```
- Running TimerApp locks its exe → stop instances (or ask owner) before rebuild.
- `dotnet build -t:Compile` on WPF is unreliable — markup compile skipped → fake CS5001/InitializeComponent errors. Use full build for truth.
- Installer outputs: `build\STM-Setup*.exe` — rebuild only when the owner asks.

## PowerShell 5.1 traps (hard rules)
- No ternary `? :`.
- Text files must be read/written with explicit UTF-8 (`[IO.File]::ReadAllText/WriteAllText`); `Get-Content` reads ANSI → once corrupted localization of uk/ru permanently.
- `Start-Process -ArgumentList` passes args as-is; regex replacements may escape braces (`\{...\}`) breaking XAML/DynamicResource — prefer Edit tool over text REPLACE in xaml.

## Conventions
- Localization: every key in all three `Localization\Strings.*.xaml` (uk/en/ru) — missing key renders «набор символов».
- Theme brushes exist in both ThemeDark/ThemeLight (add to both); code-created WPF elements cache brushes → theme change rebuilds lists.
- Data paths: `%AppData%\TimerApp` (alarms.json, app_timers.json, settings.json, Sounds\); registry Run `TimerApp` = `"exe" --autostart`.
- Installed copy lives in `%LocalAppData%\Programs\Sirko Time Manager` — patch files only from matching build output.
