using System.Windows.Threading;

namespace TimerApp.Services;

public enum ScheduleState
{
    Idle,
    Armed
}

/// <summary>
/// One-shot wall-clock scheduler: fires a single action at a chosen time of day.
/// If that time already passed today, the next occurrence (tomorrow) is used.
/// Nothing auto-runs on app launch; it arms only on explicit action.
/// </summary>
public sealed class ScheduleController
{
    private readonly DispatcherTimer _timer;
    private DateTime _target;

    public ScheduleState State { get; private set; } = ScheduleState.Idle;
    public TimeSpan TargetTime { get; private set; }

    public event Action<ScheduleState>? StateChanged;
    public event Action? Completed;

    public ScheduleController()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public void Arm(TimeSpan timeOfDay)
    {
        if (State is ScheduleState.Armed) return;

        DateTime now = DateTime.Now;
        DateTime target = now.Date + timeOfDay;
        if (target <= now) target = target.AddDays(1);

        TargetTime = timeOfDay;
        _target = target;
        _timer.Start();
        SetState(ScheduleState.Armed);
    }

    public void Cancel()
    {
        if (State is not ScheduleState.Armed) return;
        _timer.Stop();
        SetState(ScheduleState.Idle);
    }

    public void StopTimer() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        if (DateTime.Now < _target) return;
        _timer.Stop();
        SetState(ScheduleState.Idle);
        Completed?.Invoke();
    }

    private void SetState(ScheduleState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }
}
