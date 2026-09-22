using System.Windows;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Windows;

public enum UpdateDialogResult
{
    Cancel,
    Update,
    Skip
}

/// <summary>Shows the available version and its release notes; lets the user update or skip.</summary>
public partial class UpdateWindow : Window
{
    public UpdateDialogResult Result { get; private set; } = UpdateDialogResult.Cancel;

    public UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();
        VersionText.Text = $"{LocalizationService.Get("L_UpdateAvailable")}: {info.Tag}";
        NotesText.Text = string.IsNullOrWhiteSpace(info.Notes) ? "—" : info.Notes;
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        Result = UpdateDialogResult.Update;
        DialogResult = true;
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Result = UpdateDialogResult.Skip;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = UpdateDialogResult.Cancel;
        DialogResult = false;
    }
}
