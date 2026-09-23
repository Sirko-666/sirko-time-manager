using System.Windows;
using System.Windows.Threading;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Windows;

public partial class AlarmRingWindow : Window
{
    // Short sounds: solid ring, then periodic bursts until stopped.
    private static readonly TimeSpan RingDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SnoozeInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BurstDuration = TimeSpan.FromSeconds(15);

    // Long sounds: play fully once, then repeat with silence gaps until stopped.
    private static readonly TimeSpan LongSoundThreshold = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan LongSilence = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly string _soundKey;
    private readonly DispatcherTimer _ringTimer;
    private DispatcherTimer? _repeatTimer;
    private DispatcherTimer? _burstTimer;
    private TimeSpan? _duration;

    public AlarmRingWindow(Alarm alarm)
    {
        InitializeComponent();
        TimeText.Text = $"{alarm.Hour:00}:{alarm.Minute:00}";
        LabelText.Text = alarm.Label;

        // Only file sounds exist now; any legacy key falls back to the default.
        _soundKey = SoundService.IsFileSound(alarm.SoundKey) ? alarm.SoundKey : SoundService.DefaultKey;

        _ringTimer = new DispatcherTimer { Interval = RingDuration };
        _ringTimer.Tick += OnRingElapsed;

        Loaded += async (_, _) => await StartAsync();
    }

    private async Task StartAsync()
    {
        _duration = await ProbeDurationAsync();
        if (_duration is { } length && length > LongSoundThreshold)
        {
            PlayLongSound();
            return;
        }

        StartPlayback();
        _ringTimer.Start();
    }

    private void PlayLongSound()
    {
        SoundService.Play(_soundKey);
        _repeatTimer = new DispatcherTimer { Interval = _duration!.Value + LongSilence };
        _repeatTimer.Tick += OnLongRepeatTick;
        _repeatTimer.Start();
    }

    private void OnLongRepeatTick(object? sender, EventArgs e) => SoundService.Play(_soundKey);

    private async Task<TimeSpan?> ProbeDurationAsync()
    {
        Task<TimeSpan?> probe = SoundService.GetDurationAsync(_soundKey);
        Task done = await Task.WhenAny(probe, Task.Delay(ProbeTimeout));
        return ReferenceEquals(done, probe) ? probe.Result : null;
    }

    private void StartPlayback() => SoundService.PlayLooping(_soundKey);

    private void StopPlayback() => SoundService.Stop();

    private void OnRingElapsed(object? sender, EventArgs e)
    {
        _ringTimer.Stop();
        StopPlayback();

        _repeatTimer = new DispatcherTimer { Interval = SnoozeInterval };
        _repeatTimer.Tick += OnSnoozeTick;
        _repeatTimer.Start();
    }

    private void OnSnoozeTick(object? sender, EventArgs e)
    {
        StartPlayback();
        _burstTimer = new DispatcherTimer { Interval = BurstDuration };
        _burstTimer.Tick += OnBurstElapsed;
        _burstTimer.Start();
    }

    private void OnBurstElapsed(object? sender, EventArgs e)
    {
        _burstTimer?.Stop();
        StopPlayback();
    }

    private void OnStopClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _ringTimer.Stop();
        _repeatTimer?.Stop();
        _burstTimer?.Stop();
        SoundService.Stop();
        base.OnClosed(e);
    }
}
