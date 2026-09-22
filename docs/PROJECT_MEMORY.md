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
- Default install path: `%ProgramFiles%\STM` (system drive auto-detected; UAC requested automatically when needed) — since v0.1.2.
- Installer outputs: `STM-Setup-Fatty.exe` (self-contained) / `STM-Setup-Mini.exe` (framework-dependent); version source is `<Version>` in both csproj.
- Version source of truth: `<Version>` in both csproj; version text prints Major.Minor(.Build)(.Revision>0) and feeds the window title, About header, installer welcome line and Uninstall `DisplayVersion`.
- Base window 545×660 px; all tabs fit that; longer content scrolls inside its panel.
- Select-mode on cards: drag disabled, trash↔checkbox swap in fixed-width slots, selection footer always reserves height (prevents list jumps).
- Alarm sound: 1 min continuous, then 15 s every 5 min; tracks >45 s play fully once per 5 min (MediaPlayer loop via MediaEnded).
- Autostart writes `"exe" --autostart` → app launches hidden in tray; normal launch opens window.
- Theme switch rebuilds lists (FindResource caches brushes in code-created elements).
- TimeDial rework (v0.1.2.1): each wheel keeps a fractional position; visual rule is formula-based (`size = MinSize + (CenterSize-MinSize)*(1 - min(1, d/(VisibleOffsets+1)))`, `opacity` fades to 0 past ±1 row) and independent of `VisibleOffsets`; wheel and LMB-drag both supported; two-stage start ramp (3%→30% over 0.18 s, →100% over 1 s), no end inertia; centering uses ease-out. Visible pill removed; center digit is bright `InkBrush`; countdown/schedule dial frames removed in XAML.
- Custom install dirs: installer checks write access and relaunches elevated with `--dir "..."` when needed.
- Full installer = 221 MB because double runtime (installer + self-contained app) — accepted trade-off.

## Constraints
- CI on owner's box: Windows PowerShell 5.1 only (no ternary, no strict-mode JSON réunions).
- Reading text files without explicit UTF-8 corrupted localization once — always encode explicitly.
- TimerApp.exe locks itself while running — must stop before rebuild.
- Installer unsigned; SmartScreen warning is expected.
- App lock: single instance via `local\TimerApp-STM-SingleInstance` mutex; second launch signals restore.

## Successful Solutions
- Installer always re-extracts the embedded pack → updates never shadowed by cached %TEMP% copies.
- Looping alarm sound via MediaPlayer `MediaEnded` (beats 2-s timer overlap).
- Select-mode jump fix: reserved footer + `Hidden` (not `Collapsed`) visibility.
- Tray autostart: `--autostart` arg parsed in App.OnStartup; window never shown.
- Ice-red uninstall button + danger confirmation deletes app dir via detached `cmd rmdir` after 2 s.
- Dial wheel stutter fix: a burst of wheel notches was restarting the ease animation each event (accel-stop-accel + end snap). Fixed with one continuous per-column chase — notches extend an integer target, a single `CompositionTarget.Rendering` loop eases the position toward it (exponential, `WheelEaseRate`), stop threshold 0.0005 row. LMB takes over the chased column; columns are independent.

## Failed Approaches
- Self-extracting installer cached `%TEMP%\STM-Setup\app` and reused it, so newer embedded packs were ignored (installs silently kept an older app — e.g. “uninstall” button missing). Fixed: the pack is always re-extracted.
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
- Two alarms sharing same minute: only first fires (AlarmService global last-fired-minute).

## Current State
- Feature backlog: `docs\ROADMAP.md` (owner's ideas, numbered). Item #1 = in-app updates that never lose user data. Item #2 (dial UX/visual) is effectively implemented in v0.1.2.1.
- Version **0.1.2.1** (dev, not released) — csproj `<Version>` in both projects; app title/About/installer welcome/Uninstall `DisplayVersion` show 4 parts when Revision > 0. Release v0.1.1 published; v0.1.2 installers not yet rebuilt.
- csproj `<Version>` в обоих проектах — единый источник версии (title/about/installer welcome/Uninstall registry).
- Полноценный uninstaller: инсталлер пишет `HKCU\...\Uninstall\STM` (DisplayName/Version/Publisher/Icon/Size/UninstallString=`TimerApp.exe --uninstall`); приложение в режиме `--uninstall` показывает подтверждение, чистит всё и удаляет запись.
- Uninstall button lives in «Налаштування» (danger style) and in installer bottom-left; both end with topmost «програму видалено» overlay that closes on any input.
- Overlay cleanup: app schedules dir deletion via detached cmd AFTER overlay close (exe lock prevents earlier deletion).
