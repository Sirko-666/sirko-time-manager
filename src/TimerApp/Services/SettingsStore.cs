using System.IO;
using System.Text.Json;
using TimerApp.Models;

namespace TimerApp.Services;

/// <summary>Persists misc app data in %AppData%\TimerApp as JSON.</summary>
public static class SettingsStore
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimerApp");

    private static readonly string SavedCountdownFile = Path.Combine(DataDir, "saved_countdown.json");
    private static readonly string SavedScheduleFile = Path.Combine(DataDir, "saved_schedule.json");
    private static readonly string AppSettingsFile = Path.Combine(DataDir, "settings.json");
    private static readonly string AlarmsFile = Path.Combine(DataDir, "alarms.json");
    private static readonly string AppTimersFile = Path.Combine(DataDir, "app_timers.json");

    public static List<Alarm> LoadAlarms()
    {
        try
        {
            if (!File.Exists(AlarmsFile)) return [];
            var list = JsonSerializer.Deserialize<List<Alarm>>(File.ReadAllText(AlarmsFile));
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveAlarms(List<Alarm> alarms)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(AlarmsFile, JsonSerializer.Serialize(alarms));
        }
        catch
        {
            // best-effort
        }
    }

    public static List<AppTimer> LoadAppTimers()
    {
        try
        {
            if (!File.Exists(AppTimersFile)) return [];
            var list = JsonSerializer.Deserialize<List<AppTimer>>(File.ReadAllText(AppTimersFile));
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveAppTimers(List<AppTimer> items)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(AppTimersFile, JsonSerializer.Serialize(items));
        }
        catch
        {
            // best-effort
        }
    }

    private sealed class AppSettings
    {
        public string Theme { get; set; } = "dark";
        public string Language { get; set; } = "uk";
    }

    public static string LoadTheme()
    {
        try
        {
            if (!File.Exists(AppSettingsFile)) return "dark";
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppSettingsFile));
            return s?.Theme ?? "dark";
        }
        catch
        {
            return "dark";
        }
    }

    public static string LoadLanguage()
    {
        try
        {
            if (!File.Exists(AppSettingsFile)) return "uk";
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppSettingsFile));
            return s?.Language ?? "uk";
        }
        catch
        {
            return "uk";
        }
    }

    public static void Save(string theme, string language)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(AppSettingsFile,
                JsonSerializer.Serialize(new AppSettings { Theme = theme, Language = language }));
        }
        catch
        {
            // best-effort
        }
    }

    public static List<TimeSpan> LoadSavedCountdowns() => LoadTimes(SavedCountdownFile);

    public static void SaveCountdowns(List<TimeSpan> items) => SaveTimes(SavedCountdownFile, items);

    public static List<TimeSpan> LoadSavedSchedule() => LoadTimes(SavedScheduleFile);

    public static void SaveSchedule(List<TimeSpan> items) => SaveTimes(SavedScheduleFile, items);

    private static List<TimeSpan> LoadTimes(string file)
    {
        try
        {
            if (!File.Exists(file)) return [];
            var list = JsonSerializer.Deserialize<List<TimeSpan>>(File.ReadAllText(file));
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void SaveTimes(string file, List<TimeSpan> items)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(file, JsonSerializer.Serialize(items));
        }
        catch
        {
            // Persistence is best-effort; UI keeps working.
        }
    }
}