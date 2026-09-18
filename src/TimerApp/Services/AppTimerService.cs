using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using TimerApp.Models;

namespace TimerApp.Services;

public enum AppTimerAction
{
    Start,
    Stop
}

/// <summary>
/// Fires launch / close actions for applications at a chosen wall-clock time.
/// Each (entry, action) pair fires at most once per matching minute.
/// Nothing auto-runs on app launch; it polls only while the app is running.
/// </summary>
public sealed class AppTimerService
{
    private readonly DispatcherTimer _timer;
    private List<AppTimer>? _entries;
    private readonly Dictionary<string, string> _fired = new();

    public event Action<AppTimer, AppTimerAction>? Triggered;

    public AppTimerService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => Check();
    }

    public void SetEntries(List<AppTimer> entries) => _entries = entries;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void Check()
    {
        if (_entries is null) return;

        DateTime now = DateTime.Now;
        string stamp = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        foreach (AppTimer entry in _entries)
        {
            if (!entry.Enabled) continue;
            if (entry.StartEnabled)
                TryFire(entry, AppTimerAction.Start, entry.StartHour, entry.StartMinute, stamp, now);
            if (entry.StopEnabled)
                TryFire(entry, AppTimerAction.Stop, entry.StopHour, entry.StopMinute, stamp, now);
        }
    }

    private void TryFire(AppTimer entry, AppTimerAction action, int hour, int minute, string stamp, DateTime now)
    {
        if (now.Hour != hour || now.Minute != minute) return;

        string key = $"{entry.Id}:{action}";
        if (_fired.TryGetValue(key, out string? last) && last == stamp) return;

        _fired[key] = stamp;

        if (action == AppTimerAction.Start) Launch(entry);
        else Close(entry);

        Triggered?.Invoke(entry, action);
    }

    private static void Launch(AppTimer entry)
    {
        try
        {
            if (File.Exists(entry.ExePath))
                Process.Start(new ProcessStartInfo(entry.ExePath) { UseShellExecute = true });
        }
        catch
        {
            // best-effort
        }
    }

    private static void Close(AppTimer entry)
    {
        try
        {
            string name = Path.GetFileNameWithoutExtension(entry.ExePath);
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!process.CloseMainWindow())
                        process.Kill(entireProcessTree: true);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // best-effort
        }
    }
}
