using System.Diagnostics;
using System.Windows;

namespace TimerApp.Windows;

public enum UpdatePackage
{
    None,
    Fatty,
    Mini
}

/// <summary>Lets the user choose between the full (Fatty) and light (Mini) installer.</summary>
public partial class UpdateChoiceWindow : Window
{
    private const string RuntimeUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/8.0";

    public UpdatePackage Package { get; private set; } = UpdatePackage.None;

    public UpdateChoiceWindow()
    {
        InitializeComponent();
    }

    private void OnFattyClick(object sender, RoutedEventArgs e)
    {
        Package = UpdatePackage.Fatty;
        DialogResult = true;
    }

    private void OnMiniClick(object sender, RoutedEventArgs e)
    {
        Package = UpdatePackage.Mini;
        DialogResult = true;
    }

    private void OnRuntimeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(RuntimeUrl) { UseShellExecute = true });
        }
        catch
        {
            // best-effort
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Package = UpdatePackage.None;
        DialogResult = false;
    }
}
