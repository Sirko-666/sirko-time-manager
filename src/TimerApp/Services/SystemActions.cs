using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TimerApp.Services;

public enum PowerAction
{
    None,
    Sleep,
    Shutdown
}

/// <summary>Performs the requested OS action (sleep / shutdown).</summary>
public static class SystemActions
{
    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

    public static void Execute(PowerAction mode)
    {
        switch (mode)
        {
            case PowerAction.Sleep:
                SetSuspendState(false, false, false);
                break;

            case PowerAction.Shutdown:
                Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                break;

            case PowerAction.None:
                // No action requested: countdown/schedule runs without side effects.
                break;
        }
    }
}