using System.Windows.Threading;

namespace TimerApp.Services;

public enum TimerState
{
    Idle,
    Running,
    Paused
}

/// <summary>
/// Countdown state machine. Drift-free (absolute end-time based).
/// Starts only on explicit Start(); nothing auto-runs on app launch.
/// </summary>
public sealed class CountdownController
{
    private readonly DispatcherTimer _timer;
    private DateTime _endTime;
    private TimeSpan _pausedRemaining;

    public TimeSpan Current { get; private set; } = TimeSpan.Zero;
    public TimeSpan StartValue { get; private set; }
    public TimerState State { get; private set; } = TimerState.Idle;

    public event Action<TimerState>? StateChanged;
    public event Action<TimeSpan>? Updated;
    public event Action? Completed;

    public CountdownController()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += OnTick;
    }

    public void Start(TimeSpan initial)
    {
        if (State is TimerState.Running) return;

        StartValue = initial;
        Current = initial;
        Begin(RunningFromNow(initial));
        SetState(TimerState.Running);
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;
        _timer.Stop();
        _pausedRemaining = RemainingFromNow();
        SetState(TimerState.Paused);
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;
        Begin(RunningFromNow(_pausedRemaining));
        SetState(TimerState.Running);
    }

    public void Reset()
    {
        _timer.Stop();
        Current = StartValue;
        SetState(TimerState.Idle);
    }

    public void StopTimer() => _timer.Stop();

    private static DateTime RunningFromNow(TimeSpan remaining) =>
        DateTime.Now.Add(remaining);

    private TimeSpan RemainingFromNow() => _endTime - DateTime.Now;

    private void Begin(DateTime end)
    {
        _endTime = end;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        TimeSpan remaining = _endTime - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
            _timer.Stop();
            Current = remaining;
            SetState(TimerState.Idle);
            Completed?.Invoke();
            return;
        }

        int totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        TimeSpan value = TimeSpan.FromSeconds(totalSeconds);
        if (value != Current)
        {
            Current = value;
            Updated?.Invoke(Current);
        }
    }

    private void SetState(TimerState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}