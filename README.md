# ⏱ STM — Sirko Time Manager

> 🌐 **This README is available in three languages:**
> 🇬🇧 [English](#english) · 🇺🇦 [Українська](#українська) · 🇷🇺 [Русский](#русский)
>
> **Developer:** Serhii Sirenko (Sirko) · Version **0.1.3** · Windows 10/11

---

## English

A minimalistic time manager for Windows in a “dark paper” style: countdown timer, scheduling Sleep or Shutdown at an exact time, alarms with any sound, and per-app launch/close timers on schedule.

### ✨ Features

| Tab | What it does |
|---|---|
**Countdown** | iOS-style dial (mouse wheel or drag), Sleep / Shutdown action, Pause / Resume, saved timers
**Scheduled** | Sleep / Shutdown at an exact time of day; if that time already passed — tomorrow
**Alarms** | Weekday repeat, label, built-in sounds or your own WAV·MP3, ring pattern: 1 minute, then 15 s every 5 minutes until stopped
**App timers** | App cards: launch & close by schedule, 3 toggles (master / start / stop), drag-to-reorder, multi-delete
**Settings** | Interface language (Ukrainian / English / Russian), dark / light theme, start with Windows, and **uninstall the app** right from the tab (removes files, shortcuts, autostart and data with a danger-styled confirmation)

The app lives in the tray. Closing the window only hides it — timers and alarms keep running. Full exit: right-click the tray icon → **Close app**.

### 📦 Installation

Download the release that fits:

| File | Size | When to take it |
|---|---|---|
| `STM-Setup-Fatty.exe` | ~221 MB | Any PC — **no .NET needed** ✅ recommended |
| `STM-Setup-Mini.exe` | 0.5 MB | If you already have .NET 8 Desktop Runtime |

Double-click → steps: language → folder → switches (shortcuts, autostart) → Install → Done.

> ⚠️ The installer is not digitally signed: SmartScreen shows “Windows protected your PC” → *More info* → *Run anyway*.

STM is also registered in Windows **Settings → Apps → Installed apps**, where it can be uninstalled with its own uninstaller.

Details are in `README-УСТАНОВКА.md` shipped next to the installer.

### 🔧 Build from source

```powershell
# run in dev mode
dotnet build -o bin\Test
.\bin\Test\TimerApp.exe

# build installers
powershell -File make-installer.ps1         # full (self-contained)
powershell -File make-installer.ps1 -Mini   # light (framework-dependent)
```

Result: `build\STM-Setup-Fatty.exe` / `build\STM-Setup-Mini.exe`.

### 📁 Data files

| Path | Holds |
|---|---|
| `%AppData%\TimerApp\alarms.json` | alarms |
| `%AppData%\TimerApp\app_timers.json` | app timers |
| `%AppData%\TimerApp\settings.json` | language, theme |
| `%AppData%\TimerApp\Sounds\` | user-imported sounds |

### 📄 License

Free for personal use. Selling and paid redistribution are forbidden. See `LICENSE.txt`.

---

## 🇺🇦 Українська

Мінімалістичний менеджер часу для Windows у стилі «чорної бумаги»: зворотний відлік, планування Сну або Вимкнення на точний час, будильники з будь-якими звуками та таймери запуску/закриття застосунків.

### ✨ Можливості

| Вкладка | Що вміє |
|---|---|
**Зворотний відлік** | Циферблат у стилі iOS (колесо миші або перетягування), Сон / Вимкнення, Пауза / Продовжити, збережені таймери
**За розкладом** | Сон / Вимкнення на точну добу; якщо час минув — на наступний день
**Будильники** | Повтор у дні тижня, назва, вбудовані звуки або свої WAV·MP3, цикл: 1 хвилину, далі кожні 5 хвилин до «Стоп»
**Таймер застосунків** | Картки застосунків: запуск і вимкнення за розкладом, 3 перемикачі (майстерний/запуск/вимкнення), drag-сортування, масове видалення
**Налаштування** | Мова (укр / eng / рус), темна / світла тема, автозапуск із Windows і **видалення застосунку** зі вкладки (файли, ярлики, автозапуск, дані — з підтвердженням)

Застосунок живе в трeю. «Хрестик» лише ховає вікно — таймери працюють далі. Повне закриття — ПКМ по значку в треї → **«Закрити застосунок»**.

### 📦 Встановлення

`STM-Setup-Fatty.exe` (~221 МБ) — для будь-якого ПК, .NET не потрібен ✅. Або `STM-Setup-Mini.exe` (0.5 МБ) — якщо вже встановлено .NET 8 Desktop Runtime.

Двічі клацни → мова → папка → перемикачі (ярлики, автозапуск) → «Встановити» → «Готово».

> ⚠️ Інсталлер не підписаний: SmartScreen покаже «Windows захистила ПК» → *More info* → *Run anyway*.

STM також з'являється в **«Параметри → Програми → Інстальовані програми»** — з власним деінсталятором.

### 🔧 Збірка з джерел
`dotnet build -o bin\Test` → `.\bin\Test\TimerApp.exe`; інсталлери — `powershell -File make-installer.ps1 [-Mini]` → `build\`.

### 📄 Ліцензія
Використання в особистих цілях — безкоштовно. Продаж і платне поширення заборонені (LICENSE.txt).

---

## 🇷🇺 Русский

Минималистичный менеджер времени для Windows в стиле «тёмная бумага»: обратный отсчёт, планирование сна или выключения на точное время, будильники с любыми звуками и таймеры запуска/закрытия приложений по расписанию.

### ✨ Возможности

| Вкладка | Что умеет |
|---|---|
**Обратный отсчёт** | Циферблат в стиле iOS (колесо мыши или перетаскивание), Сон / Выключение, Пауза / Продолжить, сохранённые таймеры
**По расписанию** | Сон / Выключение на точное время суток; если время прошло — на следующий день
**Будильники** | Повтор по дням недели, название, встроенные звуки или свои WAV·MP3, цикл: 1 минута, затем каждые 5 минут до «Стоп»
**Таймер приложений** | Карточки приложений: запуск и закрытие по времени, 3 переключателя (мастер/запуск/выключение), перетаскивание, массовое удаление
**Настройки** | Язык (укр / eng / рус), тёмная / светлая темы, автозапуск с Windows и **удаление приложения** прямо из вкладки (файлы, ярлыки, автозапуск, данные — с подтверждением)

Приложение живёт в трее. «Крестик» только сворачивает окно — таймеры продолжают работать. Полное закрытие — ПКМ по значку в трее → **«Закрыть приложение»**.

### 📦 Установка

`STM-Setup-Fatty.exe` (~221 МБ) — для любого ПК, .NET не нужен ✅. Либо `STM-Setup-Mini.exe` (0.5 МБ) — если установлен .NET 8 Desktop Runtime.

Двойной клик → язык → папка → переключатели (ярлыки, автозапуск) → «Установить» → «Готово».

> ⚠️ Инсталлер не подписан цифровой подписью: SmartScreen покажет «Windows защитила» → *More info* → *Run anyway*.

STM также появляется в **«Параметры → Приложения → Установленные приложения»** — со своим деинсталлятором.

### 🔧 Сборка из источников
`dotnet build -o bin\Test` → `.\bin\Test\TimerApp.exe`; инсталлеры — `powershell -File make-installer.ps1 [-Mini]` → папка `build\`.

### 📄 Лицензия
Свободен для личного использования. Продажа и платное распространение запрещены (LICENSE.txt).

---

## Changelog

### v0.1.3
- **Own built-in sound library** (12 bundled sounds) instead of Windows system/Media sounds; “Add sound…” still imports your own WAV/MP3.
- **Alarm volume slider** (in Settings and in the sound picker, linked), default 60%; relative to the Windows volume.
- **Cyclic alarm repeat** (e.g. 5 working days / 2 days off) with a start date; repeat modes: once · weekdays · cycle.
- About “Features” rewritten into readable paragraphs (3 languages).

### v0.1.2.4
- Update notification is now a small toast near the tray (shown on every launch while an update is available, unless the version is skipped).
- The Settings button turns into a highlighted **"New version available"** when an update exists.
- The About block now offers **Update** (primary) and **Skip this version** buttons.

### v0.1.2.3
- Installer: the footer "Uninstall app" button is shortened to "Uninstall" so it no longer overlaps the "Back" button.

### v0.1.2.2
- **In-app updates**: hourly check of GitHub Releases with a tray notification (once per version) and a "Check for updates" button in Settings.
- **New version available** block in About with the release notes and a red "update" link; a confirmation dialog warns that running timers stop.
- Update flow: detects the .NET 8 Desktop Runtime, offers Fatty (full) or Mini (light), downloads and runs the standard installer; **all user data is preserved**.
- Installer: closes a running STM before copying files and no longer overwrites `settings.json` (keeps the chosen theme/language).

### v0.1.2.1
- **Reworked time dial** (`TimeDial`): digits now glide smoothly between rows instead of jumping; edge rows fade out toward the borders.
- **Drag input**: hold the left mouse button and move vertically to spin the dial (mouse wheel still works).
- Soft start (speed ramps in gently), no end inertia — releasing stops the dial exactly on the nearest value, which eases into the center.
- Countdown and schedule dials lost the visible frame; the whole hours/minutes column is now scrollable, not just the digits.

### v0.1.2
- STM now registers itself in Windows **Apps & Features** (Settings → Apps → Installed apps) with name, version, publisher, icon and size.
- **Proper uninstaller**: the Windows entry calls `TimerApp.exe --uninstall` — confirmation, complete cleanup (files, shortcuts, autostart, data, registry entry) and the "removed" overlay.
- Installer files renamed: `STM-Setup-Fatty.exe` (full, no .NET needed) and `STM-Setup-Mini.exe` (light).

### v0.1.1
- **Uninstall from the app**: Settings tab → "Uninstall app" (removes program files, shortcuts, autostart entry and user data after a danger-styled confirmation).
- **Uninstall from the installer**: "Uninstall app" button on the welcome page, works for custom install directories (resolved via registry/shortcut).
- **"App removed" overlay** after uninstall — topmost, centered, closes on any click or key press.
- **UAC elevation**: installing into protected folders (e.g. `C:\Program Files`) now requests admin rights automatically.
- **Reliable installer payload**: the embedded package is always re-extracted, so installers never reuse a stale cached version.
- README in three languages (English / Ukrainian / Russian).

### v0.1
- First public release: countdown, scheduled Sleep/Shutdown, alarms with custom sounds, per-app launch/close timers, tray, autostart, dark/light themes, three languages.

---

_Made by Sirko, with love for Windows 🕰️_
