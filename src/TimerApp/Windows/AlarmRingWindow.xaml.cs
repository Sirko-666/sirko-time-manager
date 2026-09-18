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
    private const int ReplayIntervalSeconds = 2;

    private readonly string _soundKey;
    private readonly bool _isFileSound;
    private readonly DispatcherTimer _ringTimer;
    private readonly DispatcherTimer? _replayTimer;
    private DispatcherTimer? _repeatTimer;
    private DispatcherTimer? _burstTimer;
    private TimeSpan? _duration;

    public AlarmRingWindow(Alarm alarm)
    {
        InitializeComponent();
        TimeText.Text = $"{alarm.Hour:00}:{alarm.Minute:00}";
        LabelText.Text = alarm.Label;
        _soundKey = alarm.SoundKey;
        _isFileSound = SoundService.IsFileSound(_soundKey);

        _ringTimer = new DispatcherTimer { Interval = RingDuration };
        _ringTimer.Tick += OnRingElapsed;

        if (!_isFileSound)
        {
            _replayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ReplayIntervalSeconds) };
            _replayTimer.Tick += (_, _) => SoundService.Play(_soundKey);
        }

        Loaded += async (_, _) => await StartAsync();
    }

    private async Task StartAsync()
    {
        if (_isFileSound)
        {
            _duration = await ProbeDurationAsync();
            if (_duration is { } length && length > LongSoundThreshold)
            {
                PlayLongSound();
                return;
            }
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

    private void StartPlayback()
    {
        if (_isFileSound)
            SoundService.PlayLooping(_soundKey);
        else
        {
            SoundService.Play(_soundKey);
            _replayTimer?.Start();
        }
    }

    private void StopPlayback()
    {
        _replayTimer?.Stop();
        SoundService.Stop();
    }

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
        _replayTimer?.Stop();
        _repeatTimer?.Stop();
        _burstTimer?.Stop();
        SoundService.Stop();
        base.OnClosed(e);
    }
}
