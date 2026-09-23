using System.Windows.Threading;
using TimerApp.Models;

namespace TimerApp.Services;

/// <summary>
/// Watches enabled alarms while the app runs and raises Triggered once per minute.
/// (Waking the PC from sleep is not handled here.)
/// </summary>
public sealed class AlarmService
{
    private readonly DispatcherTimer _timer;
    private List<Alarm> _alarms = [];
    private DateTime _lastFiredMinute = DateTime.MinValue;

    public event Action<Alarm>? Triggered;

    public AlarmService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => Check();
    }

    public void SetAlarms(List<Alarm> alarms) => _alarms = alarms;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void Check()
    {
        DateTime now = DateTime.Now;
        if (now.Minute == _lastFiredMinute.Minute && now.Hour == _lastFiredMinute.Hour &&
            now.Date == _lastFiredMinute.Date)
            return;

        foreach (Alarm alarm in _alarms)
        {
            if (!alarm.Enabled) continue;
            if (alarm.Hour != now.Hour || alarm.Minute != now.Minute) continue;
            if (!alarm.MatchesDate(now)) continue;

            _lastFiredMinute = now;
            Triggered?.Invoke(alarm);
            break;
        }
    }
}