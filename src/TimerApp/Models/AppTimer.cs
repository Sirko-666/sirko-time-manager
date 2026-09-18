namespace TimerApp.Models;

/// <summary>
/// An application entry for the "App timers" tab.
/// StartHour/Minute — when to launch the app; StopHour/Minute — when to close it.
/// </summary>
public sealed class AppTimer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExePath { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int StartHour { get; set; } = 8;
    public int StartMinute { get; set; }
    public int StopHour { get; set; } = 22;
    public int StopMinute { get; set; }
    public bool Enabled { get; set; } = true;
    public bool StartEnabled { get; set; } = true;
    public bool StopEnabled { get; set; } = true;
}
