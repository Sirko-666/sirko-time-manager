using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Windows;

public partial class AlarmEditorWindow : Window
{
    private readonly Alarm _alarm;
    private readonly bool _isNew;
    private readonly ToggleButton[] _days;
    private bool _initializing;

    private int _cycleWork;
    private int _cycleRest;
    private DateTime _cycleStart;

    public Alarm? Result { get; private set; }
    public bool Deleted { get; private set; }

    public AlarmEditorWindow(Alarm? existing)
    {
        InitializeComponent();
        _days = [Day0, Day1, Day2, Day3, Day4, Day5, Day6];

        _isNew = existing is null;
        _alarm = existing?.Clone() ?? new Alarm { Hour = 7, Minute = 0, Enabled = true };

        _cycleWork = _alarm.CycleWorkDays;
        _cycleRest = _alarm.CycleRestDays;
        _cycleStart = _alarm.CycleStartDate.Date;

        HeaderText.Text = LocalizationService.Get(_isNew ? "L_NewAlarm" : "L_EditAlarm");
        DeleteButton.Visibility = _isNew ? Visibility.Collapsed : Visibility.Visible;

        Dial.Hours = _alarm.Hour;
        Dial.Minutes = _alarm.Minute;
        LabelBox.Text = _alarm.Label;

        _initializing = true;
        for (int i = 0; i < 7; i++)
            _days[i].IsChecked = _alarm.Days[i];
        OnceToggle.IsChecked = _alarm.Mode == AlarmRepeatMode.Once;
        CycleSwitch.IsChecked = _alarm.Mode == AlarmRepeatMode.Cycle;
        _initializing = false;

        UpdateCycleButton();
        ApplyModeVisibility();
    }

    private void OnOnceChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (OnceToggle.IsChecked != true) return;

        _initializing = true;
        foreach (ToggleButton day in _days) day.IsChecked = false;
        CycleSwitch.IsChecked = false;
        _initializing = false;
        ApplyModeVisibility();
    }

    private void OnDayChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (!_days.Any(d => d.IsChecked == true)) return;

        _initializing = true;
        OnceToggle.IsChecked = false;
        CycleSwitch.IsChecked = false;
        _initializing = false;
        ApplyModeVisibility();
    }

    private void OnCycleSwitchChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        if (CycleSwitch.IsChecked == true)
        {
            _initializing = true;
            OnceToggle.IsChecked = false;
            _initializing = false;
        }
        ApplyModeVisibility();
    }

    private void ApplyModeVisibility()
    {
        bool cycle = CycleSwitch.IsChecked == true;
        WeekdayGrid.Visibility = cycle ? Visibility.Collapsed : Visibility.Visible;
        OnceToggle.Visibility = cycle ? Visibility.Collapsed : Visibility.Visible;
        CycleButton.IsEnabled = cycle;
    }

    private void UpdateCycleButton() => CycleButton.Content = $"{_cycleWork}/{_cycleRest}";

    private void OnCycleButtonClick(object sender, RoutedEventArgs e)
    {
        var window = new CycleWindow(_cycleWork, _cycleRest, _cycleStart) { Owner = this };
        if (window.ShowDialog() != true) return;

        _cycleWork = window.WorkDays;
        _cycleRest = window.RestDays;
        _cycleStart = window.StartDate;
        UpdateCycleButton();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _alarm.Hour = Dial.Hours;
        _alarm.Minute = Dial.Minutes;
        for (int i = 0; i < 7; i++)
            _alarm.Days[i] = _days[i].IsChecked == true;
        _alarm.Label = LabelBox.Text.Trim();
        _alarm.Enabled = true;

        if (CycleSwitch.IsChecked == true)
        {
            _alarm.RepeatMode = AlarmRepeatMode.Cycle;
            _alarm.CycleWorkDays = _cycleWork;
            _alarm.CycleRestDays = _cycleRest;
            _alarm.CycleStartDate = _cycleStart;
        }
        else if (OnceToggle.IsChecked == true || !_alarm.Days.Any(d => d))
        {
            _alarm.RepeatMode = AlarmRepeatMode.Once;
        }
        else
        {
            _alarm.RepeatMode = AlarmRepeatMode.Weekdays;
        }

        Result = _alarm;
        DialogResult = true;
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        Deleted = true;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
