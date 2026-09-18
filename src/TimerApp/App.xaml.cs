using System.Threading;
using System.Windows;
using TimerApp.Services;

namespace TimerApp;

public partial class App : Application
{
    private const string MutexName = @"Local\TimerApp-STM-SingleInstance";
    private const string RestoreEventName = "Local\\TimerApp-STM-Restore";

    private Mutex? _singleInstance;
    private EventWaitHandle? _restoreEvent;
    private RegisteredWaitHandle? _restoreWait;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Apply(ThemeService.FromKey(SettingsStore.LoadTheme()));
        LocalizationService.Apply(LocalizationService.FromKey(SettingsStore.LoadLanguage()));

        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is running: ask it to open its window and shut this one down.
            EventWaitHandle.OpenExisting(RestoreEventName).Set();
            Shutdown();
            return;
        }

        _restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreEventName);
        _restoreWait = ThreadPool.RegisterWaitForSingleObject(
            _restoreEvent,
            (_, timedOut) =>
            {
                if (timedOut) return;
                Dispatcher.BeginInvoke(ActivateMainWindow);
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        // Autostart launch: run silently in the tray, window stays hidden.
        bool startInTray = e.Args.Any(arg =>
            arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        var main = new MainWindow();
        MainWindow = main;
        if (!startInTray)
            main.Show();
    }

    private void ActivateMainWindow()
    {
        if (MainWindow is MainWindow main)
            main.ActivateFromTray();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _restoreWait?.Unregister(null);
        try
        {
            _singleInstance?.ReleaseMutex();
        }
        catch
        {
            // Mutex may not be owned in rare shutdown paths.
        }
        base.OnExit(e);
    }
}
