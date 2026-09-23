namespace TimerApp.Models;

/// <summary>How an alarm repeats.</summary>
public enum AlarmRepeatMode
{
    /// <summary>Fires once, then turns itself off.</summary>
    Once = 0,

    /// <summary>Fires on the selected weekdays.</summary>
    Weekdays = 1,

    /// <summary>Fires on the working days of a repeating cycle (e.g. 5/2).</summary>
    Cycle = 2
}

/// <summary>
/// A single alarm. Days array is Monday-first (0=Mon .. 6=Sun).
/// An alarm with no days selected is a one-time alarm.
/// </summary>
public sealed class Alarm
{
    public const string DefaultSoundKey = "app:Pi-li-li-li.mp3";

    public Guid Id { get; set; } = Guid.NewGuid();
    public int Hour { get; set; }
    public int Minute { get; set; }
    public bool[] Days { get; set; } = new bool[7];
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string SoundKey { get; set; } = DefaultSoundKey;

    /// <summary>
    /// Nullable for backward compatibility: alarms saved before cycles existed
    /// have no value and fall back to "weekdays if any day is set, else once".
    /// </summary>
    public AlarmRepeatMode? RepeatMode { get; set; }

    // Cycle settings (used only when Mode == Cycle): N working days then M rest days.
    public int CycleWorkDays { get; set; } = 5;
    public int CycleRestDays { get; set; } = 2;

    /// <summary>First working day of the cycle (date only).</summary>
    public DateTime CycleStartDate { get; set; } = DateTime.Today;

    public TimeSpan Time => new(Hour, Minute, 0);

    /// <summary>Effective repeat mode (infers the legacy mode when unset).</summary>
    public AlarmRepeatMode Mode =>
        RepeatMode ?? (Days.Any(d => d) ? AlarmRepeatMode.Weekdays : AlarmRepeatMode.Once);

    public bool IsRepeating => Mode != AlarmRepeatMode.Once;

    /// <summary>True when the alarm should fire on the given date.</summary>
    public bool MatchesDate(DateTime date)
    {
        switch (Mode)
        {
            case AlarmRepeatMode.Once:
                return true;

            case AlarmRepeatMode.Weekdays:
                int dayIndex = ((int)date.DayOfWeek + 6) % 7; // Monday-first
                return Days[dayIndex];

            case AlarmRepeatMode.Cycle:
                int length = Math.Max(1, CycleWorkDays + CycleRestDays);
                int diff = (date.Date - CycleStartDate.Date).Days;
                if (diff < 0) return false;
                return diff % length < Math.Max(1, CycleWorkDays);
        }

        return false;
    }

    public Alarm Clone() => new()
    {
        Id = Id,
        Hour = Hour,
        Minute = Minute,
        Days = (bool[])Days.Clone(),
        Label = Label,
        Enabled = Enabled,
        SoundKey = SoundKey,
        RepeatMode = RepeatMode,
        CycleWorkDays = CycleWorkDays,
        CycleRestDays = CycleRestDays,
        CycleStartDate = CycleStartDate
    };
}
