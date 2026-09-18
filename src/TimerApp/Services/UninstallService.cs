using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TimerApp.Services;

/// <summary>
/// All uninstall-related cleanup in one place: shortcuts, autostart,
/// user data, the "Apps &amp; Features" registry entry and the program folder.
/// </summary>
public static class UninstallService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string UninstallKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\STM";
    public const string ShortcutName = "STM — Sirko Time Manager.lnk";

    public static string? InstallDir
    {
        get
        {
            string exe = Environment.ProcessPath ?? string.Empty;
            return string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
        }
    }

    /// <summary>Removes every trace we own. Returns false when a step failed hard.</summary>
    public static bool Cleanup()
    {
        bool ok = true;
        ok &= RemoveAutostart();
        ok &= RemoveShortcuts();
        ok &= RemoveUninstallEntry();
        ok &= RemoveUserData();
        return ok;
    }

    public static void KillOtherInstances()
    {
        int self = Environment.ProcessId;
        foreach (Process process in Process.GetProcessesByName("TimerApp"))
        {
            if (process.Id == self)
            {
                process.Dispose();
                continue;
            }

            try
            {
                process.CloseMainWindow();
                process.WaitForExit(500);
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best-effort
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public static bool RemoveAutostart()
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            runKey?.DeleteValue("TimerApp", false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RemoveShortcuts()
    {
        try
        {
            string desktop = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);
            if (File.Exists(desktop)) File.Delete(desktop);

            string menu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ShortcutName);
            if (File.Exists(menu)) File.Delete(menu);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RemoveUninstallEntry()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool RemoveUserData()
    {
        try
        {
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimerApp");
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The running exe locks its own folder — remove it right after exit.</summary>
    public static void ScheduleProgramDirCleanup()
    {
        try
        {
            string? dir = InstallDir;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            string cmd = $"/c timeout /t 2 /nobreak > nul & rmdir /s /q \"{dir}\"";
            Process.Start(new ProcessStartInfo("cmd.exe", cmd)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
            // best-effort
        }
    }
}
