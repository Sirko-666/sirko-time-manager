namespace TimerApp.Models;

/// <summary>
/// A single alarm. Days array is Monday-first (0=Mon .. 6=Sun).
/// An alarm with no days selected is a one-time alarm.
/// </summary>
public sealed class Alarm
{
    public const string DefaultSoundKey = "sys:Exclamation";

    public Guid Id { get; set; } = Guid.NewGuid();
    public int Hour { get; set; }
    public int Minute { get; set; }
    public bool[] Days { get; set; } = new bool[7];
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string SoundKey { get; set; } = DefaultSoundKey;

    public TimeSpan Time => new(Hour, Minute, 0);

    public bool IsRepeating => Days.Any(d => d);

    public Alarm Clone() => new()
    {
        Id = Id,
        Hour = Hour,
        Minute = Minute,
        Days = (bool[])Days.Clone(),
        Label = Label,
        Enabled = Enabled,
        SoundKey = SoundKey
    };
}