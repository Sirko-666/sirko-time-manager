# Project Memory

> Important information that should survive future coding sessions.
> Keep this file concise. Do not store temporary details.
> Update per `.opencode/skills/project-memory/SKILL.md`. Before reversing any decision below — check this file first. Never invent historical facts.

## Architecture
- Two projects in one solution: `src\TimerApp` (WPF app) and `src\StmInstaller` (self-extracting installer; package embedded as `pack.zip` resource).
- Tabs: Зворотний відлік · За розкладом · Будильники · Таймер застосунків; top strip: Налаштування · Про застосунок.
- Base window size 545×660; About tab same size with inner ScrollViewer; scrollbars hidden globally (Styles.xaml), scroll via wheel.
- Services: CountdownController (drift-free countdown), ScheduleController (wall-clock one-shot), AlarmService (20 s polling), AppTimerService (launch/close apps), SoundService (sys+Media+user WAV/MP3), AutostartService (HKCU Run), SettingsStore (JSON in `%AppData%\TimerApp`), InstalledAppsService + ShortcutResolver (IShellLink COM).

## Important Decisions
- Base window 545×660 px; all tabs fit that; longer content scrolls inside its panel.
- Select-mode on cards: drag disabled, trash↔checkbox swap in fixed-width slots, selection footer always reserves height (prevents list jumps).
- Alarm sound: 1 min continuous, then 15 s every 5 min; tracks >45 s play fully once per 5 min (MediaPlayer loop via MediaEnded).
- Autostart writes `"exe" --autostart` → app launches hidden in tray; normal launch opens window.
- Theme switch rebuilds lists (FindResource caches brushes in code-created elements).
- Custom install dirs: installer checks write access and relaunches elevated with `--dir "..."` when needed.
- Full installer = 221 MB because double runtime (installer + self-contained app) — accepted trade-off.

## Constraints
- CI on owner's box: Windows PowerShell 5.1 only (no ternary, no strict-mode JSON réunions).
- Reading text files without explicit UTF-8 corrupted localization once — always encode explicitly.
- TimerApp.exe locks itself while running — must stop before rebuild.
- Installer unsigned; SmartScreen warning is expected.
- App lock: single instance via `local\TimerApp-STM-SingleInstance` mutex; second launch signals restore.

## Successful Solutions
- Looping alarm sound via MediaPlayer `MediaEnded` (beats 2-s timer overlap).
- Select-mode jump fix: reserved footer + `Hidden` (not `Collapsed`) visibility.
- Tray autostart: `--autostart` arg parsed in App.OnStartup; window never shown.
- Ice-red uninstall button + danger confirmation deletes app dir via detached `cmd rmdir` after 2 s.

## Failed Approaches
- Трёхрежимный свич «Неактивний/Сон/Вимкнення» во вкладках 1–2 — отклонён владельцем; вернули две радиокнопки (Сон/Вимкнення, по умолчанию Сон). `PowerAction.None` остался в enum как безопасный no-op.
- `dotnet build -t:Compile` для WPF — markup not compiled → fake errors (CS5001, missing InitializeComponent).
- `Get-Content` without UTF-8 encoding → ANSI read destroyed uk/ru localization permanently.
- FormattedText with fixed 2-s dispatcher repeat → long alarm tracks overlapped themselves.

## Dependencies
- .NET 8 WPF; WinForms interop via `<FrameworkReference Include="Microsoft.WindowsDesktop.App.WindowsForms" />` (NotifyIcon for tray).
- Shortcut COM: IShellLinkW+IPersistFile (no NuGet).
- Publishing: `make-installer.ps1` → GitHub Releases attachments only (repo holds sources).

## Known Problems
- Installer not code-signed → SmartScreen warning (plan: Azure Trusted Signing later if adoption grows).
- Uninstaller (both app Settings button and installer's «Відалити застосунок») resolves the install dir dynamically: registry Run exe path → desktop shortcut target → default %LocalAppData%\Programs path (custom install dirs covered). «Установленные приложения» entry отсуцевом (Uninstall key + uninstaller.exe — tech debt).
- Two alarms sharing same minute: only first fires (AlarmService global last-fired-minute).

## Current State
- v0.1 released on GitHub (Sirko-666/sirko-time-manager); installers rebuilt with UAC escalation + dynamic dir uninstall.
- Uninstall button lives in «Налаштування» (danger style) and in installer bottom-left; both end with topmost «програму видалено» overlay that closes on any input.
- Overlay cleanup: app schedules dir deletion via detached cmd AFTER overlay close (exe lock prevents earlier deletion).
