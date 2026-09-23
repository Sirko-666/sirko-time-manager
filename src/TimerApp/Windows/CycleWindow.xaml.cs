using System.Windows;
using System.Windows.Controls;
using TimerApp.Services;

namespace TimerApp.Windows;

/// <summary>Edits the cyclic repeat: N working days, M rest days and the start date.</summary>
public partial class CycleWindow : Window
{
    public int WorkDays { get; private set; }
    public int RestDays { get; private set; }
    public DateTime StartDate { get; private set; }

    public CycleWindow(int workDays, int restDays, DateTime startDate)
    {
        InitializeComponent();

        Wheel.LeftValue = Math.Clamp(workDays, 1, 30);
        Wheel.RightValue = Math.Clamp(restDays, 0, 30);
        StartCalendar.SelectedDate = startDate.Date;
        StartCalendar.DisplayDate = startDate.Date;

        Wheel.Changed += UpdatePreview;
        UpdatePreview();
    }

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        WorkDays = Wheel.LeftValue;
        RestDays = Wheel.RightValue;
        StartDate = (StartCalendar.SelectedDate ?? DateTime.Today).Date;

        PreviewText.Text = $"{WorkDays}/{RestDays} · {StartDate:dd.MM.yyyy}";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
