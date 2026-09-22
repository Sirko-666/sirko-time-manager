# Roadmap — planned features

> Owner's backlog of ideas. Numbered, newest ideas appended at the end.
> Statuses: `idea` · `discussed` · `planned` · `in progress` · `done` · `rejected`.
> Keep entries short: what, why, key decisions/risks. Details live in the discussion or in code.

---

## 1. In-app updates that never lose user data — `discussed`
**Goal:** STM can update itself to a newer version while keeping all user timers, alarms, app-timers, sounds and settings — permanently, in every future version.

Why it is low-risk by design:
- Program files live in `%ProgramFiles%\STM`; user data lives in `%AppData%\TimerApp\` (`alarms.json`, `app_timers.json`, `settings.json`, `Sounds\`). Updating app files never touches user data; only the full uninstaller deletes `AppData` (with confirmation).

Required groundwork:
- `schemaVersion` in JSON + migrations on startup (new fields → defaults, unknown → ignore); backup copy to `TimerApp\backup\<version>\` before migration.
- Atomic JSON writes (temp file + replace).
- Updater reads install path from `HKCU\...\Uninstall\STM` → `InstallLocation` (covers custom dirs).

Staged plan:
- **A (next version):** daily background check of GitHub Releases API, version badge + "Download" link button in About, toggle in Settings ("Автоматично перевіряти оновлення", stored in `settings.json`), always-available manual "Перевірити оновлення" button.
- **B:** one-click update — download our own installer, run `STM-Setup-Mini.exe --update`; installer closes running instance, installs into the same dir, keeps data, relaunches the app. Requires a quiet mode in our installer.
- **C (later, optional):** migrate to Velopack (delta updates, background updates, elevation handling, WPF support); trade-off — own packer `vpk`, code signing recommended.

Open questions / risks:
- Integrity of download (size/hash), HTTPS only, compare against `vX.Y.Z` tags, ignore pre-releases.
- Mini build needs .NET 8 Desktop Runtime — updater must detect it and offer `Fatty` instead.
- Portable (non-installed) copies need a file-replacement update path.
- Installers are still unsigned (SmartScreen), relevant for auto-download.

---

## 2. Revisit dial control + UX — `done`
**Goal:** improve how the countdown/schedule dial is operated and how it looks (interaction model + visual style).
Context: the dial is our own code-drawn control `src\TimerApp\Controls\TimeDial.cs` (WPF `DrawingContext`/`OnRender` — immediate-mode drawing, not XAML shapes), so there are no framework limits — only theme consistency rules (theme switch rebuilds code-created visuals) and the 545×660 window budget.

**Done (v0.1.2.1):**
- Input: mouse wheel **and** left-button vertical drag; whole hours/minutes column is a hit target, not just the digits.
- Motion: fractional per-column position (digits glide, no jumps); two-stage soft start (3%→30% over 0.18 s, →100% over 1 s); no end inertia; release eases the nearest value into the center (smoothstep); wheel uses one continuous per-column chase (no per-notch stutter/snap).
- Visuals: formula-based size/opacity independent of `VisibleOffsets` (edge rows fade to 0), smooth color/weight blend toward center; accent pill removed; center digit bright `InkBrush`; countdown/schedule dial frames removed.
- Public API unchanged: `Hours`/`Minutes`/`IsEditable`/`LargeMode`/`Changed`; `LargeMode` offset kept (2 vs 1).

Still open (future ideas): tap/keyboard entry, snap-to-5, haptics-like animation, labels/ticks/arc.

Before (0.1.2) for reference:
- Input was **mouse wheel only** (hover column by X); no drag/tap/touch/keyboard/focus.
- No snapping, no acceleration, no direct typing, no accessibility.
- Active column highlighted by an `AccentBrush` pill with `AccentTextBrush` text; hardcoded sizes.

---

## 3. _(free slot — append new ideas here)_
- Status: —
