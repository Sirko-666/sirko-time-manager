using Microsoft.Win32;

namespace TimerApp.Services;

/// <summary>Registers the app in the current-user Run key so it starts with Windows.</summary>
public static class AutostartService
{
    private const string AppName = "TimerApp";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) return;

            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return;
                key.SetValue(AppName, $"\"{exe}\" --autostart", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // best-effort.
        }
    }
}
