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

    public Alarm? Result { get; private set; }
    public bool Deleted { get; private set; }

    public AlarmEditorWindow(Alarm? existing)
    {
        InitializeComponent();
        _days = [Day0, Day1, Day2, Day3, Day4, Day5, Day6];

        _isNew = existing is null;
        _alarm = existing?.Clone() ?? new Alarm { Hour = 7, Minute = 0, Enabled = true };

        HeaderText.Text = LocalizationService.Get(_isNew ? "L_NewAlarm" : "L_EditAlarm");
        DeleteButton.Visibility = _isNew ? Visibility.Collapsed : Visibility.Visible;

        Dial.Hours = _alarm.Hour;
        Dial.Minutes = _alarm.Minute;
        for (int i = 0; i < 7; i++)
            _days[i].IsChecked = _alarm.Days[i];
        LabelBox.Text = _alarm.Label;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _alarm.Hour = Dial.Hours;
        _alarm.Minute = Dial.Minutes;
        for (int i = 0; i < 7; i++)
            _alarm.Days[i] = _days[i].IsChecked == true;
        _alarm.Label = LabelBox.Text.Trim();
        _alarm.Enabled = true;

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