using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TimerApp.Controls;
using TimerApp.Models;
using TimerApp.Services;
using TimerApp.Windows;

namespace TimerApp;

public partial class MainWindow : Window
{
    private readonly CountdownController _controller = new();
    private readonly List<TimeSpan> _saved = SettingsStore.LoadSavedCountdowns();
    private readonly ScheduleController _scheduleController = new();
    private readonly List<TimeSpan> _savedSchedule = SettingsStore.LoadSavedSchedule();
    private readonly List<Alarm> _alarms = SettingsStore.LoadAlarms();
    private readonly AlarmService _alarmService = new();
    private readonly List<AppTimer> _appTimers = SettingsStore.LoadAppTimers();
    private readonly AppTimerService _appTimerService = new();
    private readonly List<Border> _appCards = [];
    private PowerAction _mode = PowerAction.Sleep;
    private PowerAction _scheduleMode = PowerAction.Sleep;
    private bool _initializing;

    // Drag-reorder state (app cards).
    private Border? _dragCard;
    private Point _dragLast;
    private int _dragIndex;
    private double _dragOffsetY;
    private bool _dragging;
    private Point _dragStart;
    private bool _appSelectMode;
    private readonly Dictionary<Border, CheckBox> _appChecks = [];
    private readonly Dictionary<Border, StackPanel> _appWrappers = [];
    private bool _alarmSelectMode;
    private readonly Dictionary<Alarm, CheckBox> _alarmChecks = [];

    // Tray integration.
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ContextMenuStrip? _trayMenu;
    private System.Windows.Forms.ToolStripMenuItem? _trayOpenItem;
    private System.Windows.Forms.ToolStripMenuItem? _trayExitItem;
    private bool _realExit;
    private bool _balloonShown;

    public MainWindow()
    {
        InitializeComponent();
        _controller.StateChanged += OnStateChanged;
        _controller.Updated += _ => SyncDialFromController();
        _controller.Completed += OnCompleted;
        UpdateActionPanel();

        _scheduleController.StateChanged += OnScheduleStateChanged;
        _scheduleController.Completed += OnScheduleCompleted;
        UpdateScheduleActionPanel();

        _alarmService.SetAlarms(_alarms);
        _alarmService.Triggered += OnAlarmTriggered;
        _alarmService.Start();
        RebuildAlarmList();

        _appTimerService.SetEntries(_appTimers);
        _appTimerService.Start();
        RebuildAppList();

        UpdateAboutMeta();
        UpdateWindowTitle();

        ThemeService.ThemeChanged += _ => OnAppearanceChanged();
        LocalizationService.LanguageChanged += _ => OnLanguageServiceChanged();

        _initializing = true;
        ThemeBlack.IsChecked = ThemeService.Current == AppTheme.Dark;
        ThemeWhite.IsChecked = ThemeService.Current == AppTheme.Light;
        LangUk.IsChecked = LocalizationService.Current == AppLanguage.Ukrainian;
        LangEn.IsChecked = LocalizationService.Current == AppLanguage.English;
        LangRu.IsChecked = LocalizationService.Current == AppLanguage.Russian;
        AutostartSwitch.IsChecked = AutostartService.IsEnabled();
        _initializing = false;

        CreateTrayIcon();
        InitializeSize();

        Loaded += (_, _) => ApplyTitleBarTheme();
    }

    private void InitializeSize()
    {
        UpdateMinSize();
        SizeToContent = SizeToContent.Manual;
        Width = MinWidth;
        Height = MinHeight;
    }

    // -------- Theme / language --------

    private void OnThemeRadioChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        AppTheme theme = sender == ThemeWhite ? AppTheme.Light : AppTheme.Dark;
        if (theme == ThemeService.Current) return;

        ThemeService.Apply(theme);
        SaveSettings();
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        AppLanguage language = sender == LangEn ? AppLanguage.English
            : sender == LangRu ? AppLanguage.Russian
            : AppLanguage.Ukrainian;
        if (language == LocalizationService.Current) return;

        LocalizationService.Apply(language);
        SaveSettings();
    }

    private static void SaveSettings() => SettingsStore.Save(
        ThemeService.ToKey(ThemeService.Current),
        LocalizationService.ToKey(LocalizationService.Current));

    private void OnAutostartSwitchChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        AutostartService.SetEnabled(AutostartSwitch.IsChecked == true);
    }

    private void OnUninstallAppClick(object sender, RoutedEventArgs e)
    {
        var confirm = new ConfirmWindow(
            LocalizationService.Get("L_ConfirmTitle"),
            LocalizationService.Get("L_UninstallWarning"),
            dangerButton: true) { Owner = this };
        if (confirm.ShowDialog() != true) return;

        bool success = UninstallCleanup();
        if (!success) return;

        // Topmost "removed" overlay: closes on any click/keypress.
        var overlay = new Windows.UninstallDoneWindow(
            "STM",
            LocalizationService.Get("L_UninstallDoneText"),
            LocalizationService.Get("L_UninstallDoneSub"));
        overlay.ShowDialog();

        // Program dir is locked by this exe — schedule the detached cleanup now.
        ScheduleProgramDirCleanup();
        _realExit = true;
        Close();
    }

    private bool UninstallCleanup()
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ShortcutName = "STM — Sirko Time Manager.lnk";
        bool ok = true;

        try
        {
            using var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            runKey?.DeleteValue("TimerApp", false);
        }
        catch { ok = false; }

        try
        {
            string desktop = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);
            if (File.Exists(desktop)) File.Delete(desktop);

            string menu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", ShortcutName);
            if (File.Exists(menu)) File.Delete(menu);
        }
        catch { ok = false; }

        try
        {
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimerApp");
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
        }
        catch { ok = false; }

        return ok;
    }

    private void ScheduleProgramDirCleanup()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? string.Empty;
            string dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            string cmd = $"/c timeout /t 2 /nobreak > nul & rmdir /s /q \"{dir}\"";
            Process.Start(new ProcessStartInfo("cmd.exe", cmd)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch { /* best-effort */ }
    }

    private void OnAppearanceChanged()
    {
        ApplyTitleBarTheme();
        Dial.InvalidateVisual();
        ScheduleDial.InvalidateVisual();

        // Rebuild code-generated visuals so they pick up the new theme brushes.
        RebuildAlarmList();
        RebuildAppList();

        UpdateActionPanel();
        UpdateScheduleActionPanel();
        UpdateMinSize();
        EnsureContentFits(fitHeight: false);
    }

    private void OnLanguageServiceChanged()
    {
        UpdateActionPanel();
        UpdateScheduleActionPanel();
        UpdateTrayMenuTexts();
        RebuildAlarmList();
        RebuildAppList();
        UpdateAboutMeta();
        UpdateWindowTitle();
        UpdateMinSize();
        EnsureContentFits(fitHeight: false);
    }

    // -------- About tab --------

    private static string AppVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.1" : $"{version.Major}.{version.Minor}";
    }

    private void UpdateWindowTitle() =>
        Title = $"{LocalizationService.Get("L_AppTitle")}  v{AppVersion()}";

    private void UpdateAboutMeta()
    {
        AboutMeta.Text =
            $"STM · {LocalizationService.Get("L_AboutVersion")} {AppVersion()} · " +
            $"{LocalizationService.Get("L_AboutDeveloper")}: Serhii Sirenko (Sirko)";
    }

    private void UpdateMinSize()
    {
        // Base height = the app-timers tab with two full cards visible; other tabs share it
        // and scroll their lower block when content overflows.
        var panels = new FrameworkElement[] { PanelCountdown, PanelSchedule, PanelApps, PanelSettings };
        var visibility = new Visibility[panels.Length];
        for (int i = 0; i < panels.Length; i++)
        {
            visibility[i] = panels[i].Visibility;
            panels[i].Visibility = Visibility.Visible;
        }

        RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size desired = RootGrid.DesiredSize;

        for (int i = 0; i < panels.Length; i++)
            panels[i].Visibility = visibility[i];

        MinWidth = Math.Max(desired.Width, 545);
        MinHeight = Math.Max(desired.Height, 660);
        if (Width < MinWidth) Width = MinWidth;
        if (Height < MinHeight) Height = MinHeight;
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = ThemeService.Current == AppTheme.Dark ? 1 : 0;
            const int attr = 20; // DWMWA_USE_IMMERSIVE_DARK_MODE
            _ = DwmSetWindowAttribute(helper.Handle, attr, ref dark, sizeof(int));
        }
        catch
        {
            // Non-fatal: title bar keeps the system theme.
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    // -------- Tabs --------

    private void OnTabChange(object sender, RoutedEventArgs e)
    {
        var tabs = new (ToggleButton button, Grid panel)[]
        {
            (TabCountdown, PanelCountdown),
            (TabSchedule, PanelSchedule),
            (TabAlarms, PanelAlarms),
            (TabApps, PanelApps),
            (TabSettings, PanelSettings),
            (TabAbout, PanelAbout)
        };

        Grid? selected = null;
        foreach (var (button, panel) in tabs)
        {
            bool isTabSelected = button == sender;
            button.IsChecked = isTabSelected;
            panel.Visibility = isTabSelected ? Visibility.Visible : Visibility.Collapsed;
            if (isTabSelected) selected = panel;
        }

        // Every tab keeps the same base window size; taller content scrolls inside its panel.
        Height = MinHeight;
        Width = MinWidth;
        EnsureContentFits(fitHeight: false);
    }

    /// <summary>Grows the window so scrolled content of the active tab fully fits.</summary>
    private void EnsureContentFits(bool fitHeight = true)
    {
        RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size desired = RootGrid.DesiredSize;

        double chromeWidth = Math.Max(0, ActualWidth - RootGrid.ActualWidth);
        double chromeHeight = Math.Max(0, ActualHeight - RootGrid.ActualHeight);

        if (Width < desired.Width + chromeWidth) Width = desired.Width + chromeWidth;
        if (fitHeight && Height < desired.Height + chromeHeight) Height = desired.Height + chromeHeight;
        UpdateMinSize();
    }

    private PowerAction SelectedMode =>
        ModeShutdown.IsChecked == true ? PowerAction.Shutdown : PowerAction.Sleep;

    // -------- Timer actions --------

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (_controller.State is TimerState.Running) return;
        _mode = SelectedMode;
        _controller.Start(TimeSpan.FromHours(Dial.Hours).Add(TimeSpan.FromMinutes(Dial.Minutes)));
        SyncDialFromController();
    }

    private void OnPauseResumeClick(object sender, RoutedEventArgs e)
    {
        if (_controller.State == TimerState.Running)
            _controller.Pause();
        else
            _controller.Resume();

        SyncDialFromController();
    }

    private void OnResetClick(object sender, RoutedEventArgs e) => _controller.Reset();

    private void OnStateChanged(TimerState state)
    {
        UpdateActionPanel();
        Dial.IsEditable = state != TimerState.Running;
        if (state is TimerState.Running or TimerState.Idle)
            SyncDialFromController();
    }

    private void UpdateActionPanel()
    {
        ActionPanel.Children.Clear();

        switch (_controller.State)
        {
            case TimerState.Running:
                ActionPanel.Children.Add(MakePrimary(LocalizationService.Get("L_Pause"), OnPauseResumeClick));
                ActionPanel.Children.Add(MakeGhost(LocalizationService.Get("L_Reset"), OnResetClick, leftMargin: 8));
                SavedPanel.IsEnabled = false;
                break;

            case TimerState.Paused:
                ActionPanel.Children.Add(MakePrimary(LocalizationService.Get("L_Resume"), OnPauseResumeClick));
                ActionPanel.Children.Add(MakeGhost(LocalizationService.Get("L_Reset"), OnResetClick, leftMargin: 8));
                SavedPanel.IsEnabled = true;
                break;

            default:
                ActionPanel.Children.Add(MakePrimary(LocalizationService.Get("L_Start"), OnStartClick));
                SavedPanel.IsEnabled = true;
                break;
        }
    }

    private static Button MakePrimary(string text, RoutedEventHandler handler)
    {
        var b = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("PrimaryButtonStyle")
        };
        b.Click += handler;
        return b;
    }

    private static Button MakeGhost(string text, RoutedEventHandler handler, int leftMargin)
    {
        var b = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("GhostButtonStyle"),
            Margin = new Thickness(leftMargin, 0, 0, 0)
        };
        b.Click += handler;
        return b;
    }

    private void SyncDialFromController()
    {
        Dial.Hours = (int)Math.Min(_controller.Current.TotalHours, 23);
        Dial.Minutes = _controller.Current.Minutes;
    }

    // -------- Saved timers --------

    private void OnSaveCurrentClick(object sender, RoutedEventArgs e)
    {
        var value = TimeSpan.FromHours(Dial.Hours).Add(TimeSpan.FromMinutes(Dial.Minutes));
        if (value <= TimeSpan.Zero) return;

        _saved.Add(value);
        SettingsStore.SaveCountdowns(_saved);
    }

    private void OnSavedClick(object sender, RoutedEventArgs e)
    {
        var picker = new SavedTimersWindow(_saved) { Owner = this };
        if (picker.ShowDialog() == true && picker.Picked is { } picked)
        {
            Dial.Hours = (int)Math.Min(picked.TotalHours, 23);
            Dial.Minutes = picked.Minutes;
        }
        SettingsStore.SaveCountdowns(_saved);
    }

    // -------- Schedule --------

    private PowerAction SelectedScheduleMode =>
        ScheduleModeShutdown.IsChecked == true ? PowerAction.Shutdown : PowerAction.Sleep;

    private static string ModeName(PowerAction mode) =>
        LocalizationService.Get(mode == PowerAction.Shutdown ? "L_ModeShutdown"
            : mode == PowerAction.Sleep ? "L_ModeSleep"
            : "L_ModeNone");

    private void OnScheduleClick(object sender, RoutedEventArgs e)
    {
        if (_scheduleController.State is ScheduleState.Armed) return;
        _scheduleMode = SelectedScheduleMode;
        _scheduleController.Arm(TimeSpan.FromHours(ScheduleDial.Hours)
            .Add(TimeSpan.FromMinutes(ScheduleDial.Minutes)));
    }

    private void OnScheduleCancelClick(object sender, RoutedEventArgs e) => _scheduleController.Cancel();

    private void OnScheduleStateChanged(ScheduleState state)
    {
        ScheduleDial.IsEditable = state != ScheduleState.Armed;
        UpdateScheduleActionPanel();
        UpdateMinSize();
    }

    private void UpdateScheduleActionPanel()
    {
        ScheduleActionPanel.Children.Clear();

        if (_scheduleController.State is ScheduleState.Armed)
        {
            ScheduleStatus.Text =
                $"{LocalizationService.Get("L_ScheduledFor")} " +
                $"{SavedTimersWindow.Format(_scheduleController.TargetTime)} · {ModeName(_scheduleMode)}";
            ScheduleStatus.Visibility = Visibility.Visible;
            ScheduleModePanel.IsEnabled = false;
            ScheduleSavedPanel.IsEnabled = false;
            ScheduleActionPanel.Children.Add(
                MakePrimary(LocalizationService.Get("L_Cancel"), OnScheduleCancelClick));
        }
        else
        {
            ScheduleStatus.Visibility = Visibility.Collapsed;
            ScheduleModePanel.IsEnabled = true;
            ScheduleSavedPanel.IsEnabled = true;
            ScheduleActionPanel.Children.Add(
                MakePrimary(LocalizationService.Get("L_Schedule"), OnScheduleClick));
        }
    }

    private void OnScheduleCompleted()
    {
        if (_scheduleMode == PowerAction.None) return;
        SystemActions.Execute(_scheduleMode);
    }

    private void OnScheduleSaveCurrentClick(object sender, RoutedEventArgs e)
    {
        _savedSchedule.Add(TimeSpan.FromHours(ScheduleDial.Hours)
            .Add(TimeSpan.FromMinutes(ScheduleDial.Minutes)));
        SettingsStore.SaveSchedule(_savedSchedule);
    }

    private void OnScheduleSavedClick(object sender, RoutedEventArgs e)
    {
        var picker = new SavedTimersWindow(_savedSchedule) { Owner = this };
        if (picker.ShowDialog() == true && picker.Picked is { } picked)
        {
            ScheduleDial.Hours = (int)Math.Min(picked.TotalHours, 23);
            ScheduleDial.Minutes = picked.Minutes;
        }
        SettingsStore.SaveSchedule(_savedSchedule);
    }

    // -------- Alarms --------

    private void RebuildAlarmList()
    {
        AlarmList.Children.Clear();
        _alarmChecks.Clear();
        bool empty = _alarms.Count == 0;
        NoAlarmsHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        AlarmList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        foreach (Alarm alarm in _alarms.OrderBy(a => a.Time))
            AlarmList.Children.Add(BuildAlarmRow(alarm));

        UpdateAlarmSelectionFooter();
    }

    private UIElement BuildAlarmRow(Alarm alarm)
    {
        var time = new TextBlock
        {
            Text = $"{alarm.Hour:00}:{alarm.Minute:00}",
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("InkBrush")
        };
        var sub = new TextBlock
        {
            Text = DescribeAlarm(alarm),
            FontSize = 12,
            Foreground = (Brush)FindResource("MutedTextBrush"),
            MaxWidth = 200,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var info = new StackPanel
        {
            // Fixed width: every alarm row has identical width, so centered rows align.
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Opacity = alarm.Enabled ? 1.0 : 0.45
        };
        info.Children.Add(time);
        info.Children.Add(sub);
        info.MouseLeftButtonDown += (_, _) =>
        {
            if (_alarmSelectMode && _alarmChecks.TryGetValue(alarm, out CheckBox? rowCheck))
            {
                rowCheck.IsChecked = rowCheck.IsChecked != true;
                return;
            }
            EditAlarm(alarm);
        };

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("SwitchStyle"),
            IsChecked = alarm.Enabled,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.Checked += (_, _) => SetAlarmEnabled(alarm, true);
        toggle.Unchecked += (_, _) => SetAlarmEnabled(alarm, false);

        var sound = new Button
        {
            Style = (Style)FindResource("SoundButtonStyle"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = LocalizationService.Get("L_AlarmSound")
        };
        sound.Click += (_, _) => PickAlarmSound(alarm);

        var grid = new Grid
        {
            Margin = new Thickness(0, 8, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(info, 0);
        Grid.SetColumn(sound, 1);
        Grid.SetColumn(toggle, 2);
        grid.Children.Add(info);
        grid.Children.Add(sound);
        grid.Children.Add(toggle);

        // Left slot: checkbox (selection mode) or trash (normal mode).
        var alarmCheck = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        var alarmTrash = new Button
        {
            Style = (Style)FindResource("TrashButtonStyle"),
            ToolTip = LocalizationService.Get("L_Delete"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        alarmTrash.Click += (_, _) => ConfirmDeleteAlarm(alarm);

        var slot = new Grid
        {
            Width = 30,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(alarmTrash, 0);
        slot.Children.Add(alarmCheck);
        slot.Children.Add(alarmTrash);

        _alarmChecks[alarm] = alarmCheck;
        UpdateAlarmSlotVisibility(alarmCheck, alarmTrash);

        var wrapper = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        wrapper.Children.Add(slot);
        wrapper.Children.Add(grid);
        return wrapper;
    }

    private void UpdateAlarmSlotVisibility(CheckBox check, Button trash)
    {
        check.Visibility = _alarmSelectMode ? Visibility.Visible : Visibility.Collapsed;
        trash.Visibility = _alarmSelectMode ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateAlarmSelectionFooter()
    {
        // Hidden keeps the reserved footer height so the alarm list does not jump.
        Visibility state = _alarmSelectMode ? Visibility.Visible : Visibility.Hidden;
        AlarmSelectAllSwitch.Visibility = state;
        AlarmSelectAllLabel.Visibility = state;
        AlarmDeleteSelectedButton.Visibility = state;
    }

    private void OnAlarmSelectClick(object sender, RoutedEventArgs e)
    {
        _alarmSelectMode = !_alarmSelectMode;
        UpdateAlarmSelectionFooter();
        RebuildAlarmList();
    }

    private void OnAlarmSelectAllChanged(object sender, RoutedEventArgs e)
    {
        bool selectAll = AlarmSelectAllSwitch.IsChecked == true;
        foreach (CheckBox check in _alarmChecks.Values)
            check.IsChecked = selectAll;
    }

    private void OnAlarmDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = _alarmChecks
            .Where(pair => pair.Value.IsChecked == true)
            .Select(pair => pair.Key)
            .ToList();
        if (selected.Count == 0) return;

        var confirm = new ConfirmWindow(
            LocalizationService.Get("L_ConfirmTitle"),
            LocalizationService.Get("L_DeleteAlarmsConfirm")) { Owner = this };
        if (confirm.ShowDialog() != true) return;

        var ids = selected.Select(a => a.Id).ToHashSet();
        _alarms.RemoveAll(a => ids.Contains(a.Id));
        SettingsStore.SaveAlarms(_alarms);
        _alarmSelectMode = false;
        UpdateAlarmSelectionFooter();
        RebuildAlarmList();
    }

    private void ConfirmDeleteAlarm(Alarm alarm)
    {
        var confirm = new ConfirmWindow(
            LocalizationService.Get("L_ConfirmTitle"),
            string.Format(LocalizationService.Get("L_DeleteAlarmConfirm"),
                $"{alarm.Hour:00}:{alarm.Minute:00}")) { Owner = this };
        if (confirm.ShowDialog() != true) return;

        _alarms.RemoveAll(a => a.Id == alarm.Id);
        SettingsStore.SaveAlarms(_alarms);
        RebuildAlarmList();
    }

    private void PickAlarmSound(Alarm alarm)
    {
        var picker = new SoundPickerWindow(alarm.SoundKey) { Owner = this };
        if (picker.ShowDialog() != true || picker.Result is not { } key) return;

        alarm.SoundKey = key;
        SettingsStore.SaveAlarms(_alarms);
        RebuildAlarmList();
    }

    private void SetAlarmEnabled(Alarm alarm, bool enabled)
    {
        if (alarm.Enabled == enabled) return;
        alarm.Enabled = enabled;
        SettingsStore.SaveAlarms(_alarms);
        RebuildAlarmList();
    }

    private string DescribeAlarm(Alarm alarm)
    {
        string days;
        if (!alarm.IsRepeating)
            days = LocalizationService.Get("L_DaysOnce");
        else if (alarm.Days.All(d => d))
            days = LocalizationService.Get("L_DaysEveryday");
        else if (alarm.Days[0] && alarm.Days[1] && alarm.Days[2] && alarm.Days[3] && alarm.Days[4] &&
                 !alarm.Days[5] && !alarm.Days[6])
            days = LocalizationService.Get("L_DaysWorkdays");
        else if (!alarm.Days[0] && !alarm.Days[1] && !alarm.Days[2] && !alarm.Days[3] && !alarm.Days[4] &&
                 alarm.Days[5] && alarm.Days[6])
            days = LocalizationService.Get("L_DaysWeekends");
        else
        {
            string[] keys = ["L_DayMon", "L_DayTue", "L_DayWed", "L_DayThu", "L_DayFri", "L_DaySat", "L_DaySun"];
            days = string.Join(", ", Enumerable.Range(0, 7)
                .Where(i => alarm.Days[i])
                .Select(i => LocalizationService.Get(keys[i])));
        }

        return string.IsNullOrEmpty(alarm.Label) ? days : $"{days} · {alarm.Label}";
    }

    private void OnAddAlarmClick(object sender, RoutedEventArgs e)
    {
        var editor = new AlarmEditorWindow(null) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } created)
        {
            _alarms.Add(created);
            SettingsStore.SaveAlarms(_alarms);
            RebuildAlarmList();
        }
    }

    private void EditAlarm(Alarm alarm)
    {
        var editor = new AlarmEditorWindow(alarm) { Owner = this };
        if (editor.ShowDialog() != true) return;

        if (editor.Deleted)
            _alarms.RemoveAll(a => a.Id == alarm.Id);
        else if (editor.Result is { } updated)
        {
            int index = _alarms.FindIndex(a => a.Id == alarm.Id);
            if (index >= 0) _alarms[index] = updated;
        }

        SettingsStore.SaveAlarms(_alarms);
        RebuildAlarmList();
    }

    private void OnAlarmTriggered(Alarm alarm)
    {
        if (!alarm.IsRepeating)
        {
            alarm.Enabled = false;
            SettingsStore.SaveAlarms(_alarms);
            RebuildAlarmList();
        }

        new AlarmRingWindow(alarm) { Owner = this }.Show();
    }

    // -------- App timers --------

    private void RebuildAppList()
    {
        _appCards.Clear();
        _appChecks.Clear();
        _appWrappers.Clear();
        AppCardList.Children.Clear();
        bool empty = _appTimers.Count == 0;
        NoAppsHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        AppScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        foreach (AppTimer entry in _appTimers)
            AddAppCard(entry);

        UpdateAppPanelSize();
    }

    /// <summary>Limits the scroll area to exactly 1 (empty/one) or 2 visible cards.</summary>
    private void UpdateAppPanelSize()
    {
        double step = 0;
        if (_appCards.Count > 0)
        {
            Border card = _appCards[0];
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            step = card.DesiredSize.Height + card.Margin.Top + card.Margin.Bottom;
        }

        double visibleRows = Math.Min(_appCards.Count, 2);
        AppScroll.MaxHeight = visibleRows * step;

        UpdateMinSize();
    }

    private void AddAppCard(AppTimer entry)
    {
        Border card = BuildAppCard(entry);

        // Left slot hosts the checkbox (selection mode) or the trash button
        // (normal mode) — same fixed width, card position never changes.
        var slot = new Grid
        {
            Width = 30,
            VerticalAlignment = VerticalAlignment.Center
        };

        var check = _appChecks[card];
        check.Margin = new Thickness(0);
        check.HorizontalAlignment = HorizontalAlignment.Center;
        check.VerticalAlignment = VerticalAlignment.Center;
        slot.Children.Add(check);

        var trash = new Button
        {
            Style = (Style)FindResource("TrashButtonStyle"),
            ToolTip = LocalizationService.Get("L_Delete"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        trash.Click += (_, _) => ConfirmDeleteApp(entry);
        Grid.SetColumn(trash, 0);
        slot.Children.Add(trash);

        UpdateSlotVisibility(check, trash);

        var wrapper = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        wrapper.Children.Add(slot);
        wrapper.Children.Add(card);

        _appWrappers[card] = wrapper;
        _appCards.Add(card);
        AppCardList.Children.Add(wrapper);
    }

    private void UpdateSlotVisibility(CheckBox check, Button trash)
    {
        // Pressing "Вибрати" swaps trash buttons for checkboxes.
        check.Visibility = _appSelectMode ? Visibility.Visible : Visibility.Collapsed;
        trash.Visibility = _appSelectMode ? Visibility.Collapsed : Visibility.Visible;
    }

    private Border BuildAppCard(AppTimer entry)
    {
        var icon = GetAppIcon(entry.ExePath);
        var iconHost = new Border { Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon is not null)
        {
            iconHost.Child = new Image
            {
                Source = icon,
                Width = 32,
                Height = 32,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            var letter = new TextBlock
            {
                Text = entry.Label.Length > 0 ? entry.Label[..1].ToUpper() : "?",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("InkBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconHost.Background = (Brush)FindResource("PanelHoverBrush");
            iconHost.Child = letter;
        }

        var name = new TextBlock
        {
            Text = entry.Label,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("InkBrush"),
            Width = 128,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var selectCheck = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var masterSwitch = new ToggleButton
        {
            Style = (Style)FindResource("SwitchStyle"),
            IsChecked = entry.Enabled,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        masterSwitch.Checked += (_, _) => SetAppEntryEnabled(entry, true);
        masterSwitch.Unchecked += (_, _) => SetAppEntryEnabled(entry, false);

        // Column 0: icon + name + master switch — fixed width so all cards match.
        var infoColumn = new StackPanel
        {
            Width = 128,
            VerticalAlignment = VerticalAlignment.Center
        };
        infoColumn.Children.Add(iconHost);
        infoColumn.Children.Add(name);
        infoColumn.Children.Add(masterSwitch);
        Grid.SetColumn(infoColumn, 0);

        var startDial = new TimeDial();
        startDial.Hours = entry.StartHour;
        startDial.Minutes = entry.StartMinute;
        startDial.Changed += () =>
        {
            entry.StartHour = startDial.Hours;
            entry.StartMinute = startDial.Minutes;
            SettingsStore.SaveAppTimers(_appTimers);
        };
        var startCell = MakeDialCell(
            LocalizationService.Get("L_AppStartTimer"), startDial, entry.StartEnabled,
            value =>
            {
                entry.StartEnabled = value;
                SettingsStore.SaveAppTimers(_appTimers);
            });
        Grid.SetColumn(startCell, 1);

        var stopDial = new TimeDial();
        stopDial.Hours = entry.StopHour;
        stopDial.Minutes = entry.StopMinute;
        stopDial.Changed += () =>
        {
            entry.StopHour = stopDial.Hours;
            entry.StopMinute = stopDial.Minutes;
            SettingsStore.SaveAppTimers(_appTimers);
        };
        var stopCell = MakeDialCell(
            LocalizationService.Get("L_AppStopTimer"), stopDial, entry.StopEnabled,
            value =>
            {
                entry.StopEnabled = value;
                SettingsStore.SaveAppTimers(_appTimers);
            });
        Grid.SetColumn(stopCell, 2);

        var card = new Border
        {
            Background = (Brush)FindResource("PanelBrush"),
            BorderBrush = (Brush)FindResource("HairlineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 10, 10, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = _appSelectMode ? Cursors.Hand : Cursors.SizeAll,
            Opacity = entry.Enabled ? 1.0 : 0.45,
            Tag = entry
        };

        var grid = new Grid();
        for (int i = 0; i < 3; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(infoColumn);
        grid.Children.Add(startCell);
        grid.Children.Add(stopCell);

        card.Child = grid;

        card.MouseLeftButtonDown += OnAppCardDown;
        card.MouseMove += OnAppCardMove;
        card.MouseLeftButtonUp += OnAppCardUp;
        card.LostMouseCapture += OnAppCardLostCapture;

        _appChecks[card] = selectCheck;

        return card;
    }

    private static UIElement MakeDialCell(string label, TimeDial dial, bool toggleChecked, Action<bool> onToggle)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("MutedTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };

        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.FindResource("SwitchStyle"),
            IsChecked = toggleChecked,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        toggle.Checked += (_, _) => onToggle(true);
        toggle.Unchecked += (_, _) => onToggle(false);

        var stack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        stack.Children.Add(text);
        stack.Children.Add(dial);
        stack.Children.Add(toggle);
        return stack;
    }

    private void SetAppEntryEnabled(AppTimer entry, bool enabled)
    {
        if (entry.Enabled == enabled) return;
        entry.Enabled = enabled;
        SettingsStore.SaveAppTimers(_appTimers);
        RebuildAppList();
    }

    private void OnAddAppClick(object sender, RoutedEventArgs e)
    {
        var picker = new AppPickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.PickedPath is null) return;
        AddAppEntry(ShortcutResolver.ResolveOrSelf(picker.PickedPath));
    }

    private void OnBrowseAppClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("L_FindApp"),
            Filter = "Programs|*.exe;*.lnk|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        AddAppEntry(ShortcutResolver.ResolveOrSelf(dialog.FileName));
    }

    private void AddAppEntry(string sourcePath)
    {
        var entry = new AppTimer
        {
            ExePath = sourcePath,
            Label = DisplayNameOf(sourcePath)
        };
        _appTimers.Add(entry);
        SettingsStore.SaveAppTimers(_appTimers);
        RebuildAppList();
    }

    private void PersistAppOrder()
    {
        var ordered = _appCards.Select(c => (AppTimer)c.Tag!).ToList();
        _appTimers.Clear();
        foreach (AppTimer entry in ordered)
            _appTimers.Add(entry);
        _appTimerService.SetEntries(_appTimers);
        SettingsStore.SaveAppTimers(_appTimers);
    }

    private static string DisplayNameOf(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                return info.FileDescription!;
        }
        catch
        {
            // fall back below
        }
        return Path.GetFileNameWithoutExtension(exePath);
    }

    private static ImageSource? GetAppIcon(string exePath)
    {
        try
        {
            var large = new IntPtr[1];
            if (ExtractIconEx(exePath, 0, large, null, 1) == 0 || large[0] == IntPtr.Zero)
                return null;

            var source = Imaging.CreateBitmapSourceFromHIcon(
                large[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(large[0]);
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string lpszFile, int nIconIndex, IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // -------- App card removal / selection --------

    private void ConfirmDeleteApp(AppTimer entry)
    {
        var confirm = new ConfirmWindow(
            LocalizationService.Get("L_ConfirmTitle"),
            string.Format(LocalizationService.Get("L_DeleteAppConfirm"), entry.Label)) { Owner = this };
        if (confirm.ShowDialog() != true) return;

        _appTimers.RemoveAll(a => a.Id == entry.Id);
        SettingsStore.SaveAppTimers(_appTimers);
        RebuildAppList();
    }

    private void OnAppSelectClick(object sender, RoutedEventArgs e)
    {
        _appSelectMode = !_appSelectMode;
        UpdateSelectionFooter();

        // Never allow reordering while selection mode is active.
        if (_dragCard is not null) EndDragCore(_dragCard);
        _dragCard = null;
        _dragging = false;

        RebuildAppList();
    }

    private void UpdateSelectionFooter()
    {
        // Hidden keeps the reserved footer height so the card list does not jump.
        Visibility state = _appSelectMode ? Visibility.Visible : Visibility.Hidden;
        AppSelectAllSwitch.Visibility = state;
        AppSelectAllLabel.Visibility = state;
        AppDeleteSelectedButton.Visibility = state;
    }

    private void OnAppSelectAllChanged(object sender, RoutedEventArgs e)
    {
        bool selectAll = AppSelectAllSwitch.IsChecked == true;
        foreach (CheckBox check in _appChecks.Values)
            check.IsChecked = selectAll;
    }

    private void OnAppDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = _appChecks
            .Where(pair => pair.Value.IsChecked == true)
            .Select(pair => (AppTimer)pair.Key.Tag!)
            .ToList();
        if (selected.Count == 0) return;

        var confirm = new ConfirmWindow(
            LocalizationService.Get("L_ConfirmTitle"),
            LocalizationService.Get("L_DeleteAppsConfirm")) { Owner = this };
        if (confirm.ShowDialog() != true) return;

        var ids = selected.Select(a => a.Id).ToHashSet();
        _appTimers.RemoveAll(a => ids.Contains(a.Id));
        SettingsStore.SaveAppTimers(_appTimers);
        _appSelectMode = false;
        UpdateSelectionFooter();
        RebuildAppList();
    }

    // -------- App card drag-reorder --------

    private void OnAppCardDown(object sender, MouseButtonEventArgs e)
    {
        var card = (Border)sender;
        if (e.OriginalSource is ButtonBase) return; // switches / buttons stay clickable

        // In select mode a card click toggles its checkbox instead of dragging.
        if (_appSelectMode)
        {
            _dragCard = null;
            _dragging = false;
            if (_appChecks.TryGetValue(card, out CheckBox? check) && check is not null)
                check.IsChecked = check.IsChecked != true;
            e.Handled = true;
            return;
        }

        if (_dragCard is not null) return;

        _dragCard = card;
        _dragStart = e.GetPosition(AppCardList);
        _dragLast = _dragStart;
    }

    private void OnAppCardMove(object sender, MouseEventArgs e)
    {
        if (_appSelectMode) return;
        var card = (Border)sender;
        if (card != _dragCard) return;

        Point pos = e.GetPosition(AppCardList);

        if (!_dragging)
        {
            if (Math.Abs(pos.Y - _dragStart.Y) < 6) return;
            StartDrag(card);
        }

        _dragOffsetY += pos.Y - _dragLast.Y;
        _dragLast = pos;

        double step = card.ActualHeight + card.Margin.Top + card.Margin.Bottom;

        while (_dragOffsetY > step / 2 && _dragIndex < _appCards.Count - 1)
            SwapWithNeighbor(card, +1, step);
        while (_dragOffsetY < -step / 2 && _dragIndex > 0)
            SwapWithNeighbor(card, -1, step);

        if (card.RenderTransform is TranslateTransform translate)
            translate.Y = _dragOffsetY;
    }

    private void StartDrag(Border card)
    {
        _dragging = true;
        _dragIndex = _appCards.IndexOf(card);
        _dragOffsetY = 0;
        card.RenderTransform = new TranslateTransform();
        card.CaptureMouse();
    }

    private void SwapWithNeighbor(Border card, int direction, double step)
    {
        int neighborIndex = _dragIndex + direction;
        StackPanel wrapper = _appWrappers[card];
        AppCardList.Children.Remove(wrapper);
        AppCardList.Children.Insert(neighborIndex, wrapper);
        _appCards.Remove(card);
        _appCards.Insert(neighborIndex, card);
        _dragIndex = neighborIndex;
        _dragOffsetY -= step * direction;
    }

    private void OnAppCardUp(object sender, MouseButtonEventArgs e)
    {
        var card = (Border)sender;
        if (card != _dragCard) return;
        EndDrag(card);
    }

    private void OnAppCardLostCapture(object sender, MouseEventArgs e) => EndDragCore((Border)sender);

    private void EndDrag(Border card)
    {
        if (!_dragging)
        {
            _dragCard = null;
            return;
        }
        EndDragCore(card);
        if (card.IsMouseCaptured) card.ReleaseMouseCapture();
    }

    private void EndDragCore(Border card)
    {
        if (card.RenderTransform is TranslateTransform translate)
        {
            card.LayoutUpdated -= (_, _) => { };
            card.RenderTransform = null;
        }

        if (_dragging)
        {
            _dragging = false;
            PersistAppOrder();
        }
        _dragCard = null;
    }

    private void OnCompleted()
    {
        Dial.IsEditable = true;
        Dial.Hours = 0;
        Dial.Minutes = 0;
        Dial.InvalidateVisual();

        if (_mode == PowerAction.None) return;

        // Act immediately: no confirmation dialog.
        SystemActions.Execute(_mode);
    }

    // -------- Tray --------

    private void CreateTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "STM — Sirko Time Manager",
            Visible = true
        };
        try
        {
            var stream = Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app.ico")).Stream;
            _trayIcon.Icon = new System.Drawing.Icon(stream);
        }
        catch
        {
            // Icon resource missing: tray uses the default app icon.
        }

        _trayMenu = new System.Windows.Forms.ContextMenuStrip();
        _trayOpenItem = _trayMenu.Items.Add(LocalizationService.Get("L_TrayOpen")) as System.Windows.Forms.ToolStripMenuItem;
        if (_trayOpenItem is not null)
            _trayOpenItem.Click += (_, _) => ActivateFromTray();
        _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _trayExitItem = _trayMenu.Items.Add(LocalizationService.Get("L_TrayExit")) as System.Windows.Forms.ToolStripMenuItem;
        if (_trayExitItem is not null)
            _trayExitItem.Click += (_, _) => ExitToDesktop();

        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.DoubleClick += (_, _) => ActivateFromTray();
        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                ActivateFromTray();
        };
    }

    private void UpdateTrayMenuTexts()
    {
        if (_trayOpenItem is not null)
            _trayOpenItem.Text = LocalizationService.Get("L_TrayOpen");
        if (_trayExitItem is not null)
            _trayExitItem.Text = LocalizationService.Get("L_TrayExit");
    }

    public void ActivateFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitToDesktop()
    {
        _realExit = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_realExit)
        {
            // Collapsing into tray: the app keeps working (timers, alarms...).
            e.Cancel = true;
            Hide();
            if (_trayIcon is not null && !_balloonShown)
            {
                _trayIcon.BalloonTipText = LocalizationService.Get("L_TrayBalloon");
                _trayIcon.BalloonTipTitle = "STM";
                _trayIcon.ShowBalloonTip(2500);
                _balloonShown = true;
            }
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _controller.StopTimer();
        _scheduleController.StopTimer();
        _alarmService.Stop();
        _appTimerService.Stop();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        base.OnClosed(e);
    }
}