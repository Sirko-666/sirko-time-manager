using System.IO;

namespace TimerApp.Services;

public sealed record InstalledApp(string Name, string ShortcutPath);

/// <summary>Enumerates installed applications via Start Menu shortcuts.</summary>
public static class InstalledAppsService
{
    public static List<InstalledApp> GetInstalledApps()
    {
        var apps = new List<InstalledApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // User Menu first so its names take priority over the shared ones.
        Walk(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), apps, seen);
        Walk(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"), apps, seen);

        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Walk(string dir, List<InstalledApp> apps, HashSet<string> seen)
    {
        try
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            foreach (string file in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                apps.Add(new InstalledApp(name, file));
            }

            foreach (string sub in Directory.EnumerateDirectories(dir))
            {
                string folder = Path.GetFileName(sub);
                if (string.Equals(folder, "Startup", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(sub, apps, seen);
            }
        }
        catch
        {
            // Inaccessible folder: skip it, keep the rest.
        }
    }
}
