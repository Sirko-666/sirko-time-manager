using System.IO;
using System.Text.Json;

namespace TimerApp.Services;

/// <summary>
/// Update preferences, kept in a separate %AppData%\TimerApp\update.json so the
/// existing settings.json schema stays untouched.
/// </summary>
public sealed class UpdateSettings
{
    public bool AutoCheck { get; set; } = true;

    /// <summary>Version the user chose to skip (never auto-offered again).</summary>
    public string? SkippedVersion { get; set; }

    /// <summary>UTC timestamp (round-trip "o") of the last successful check.</summary>
    public string? LastCheckUtc { get; set; }
}

public static class UpdateStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimerApp", "update.json");

    public static UpdateSettings Load()
    {
        try
        {
            if (!System.IO.File.Exists(FilePath)) return new UpdateSettings();
            return JsonSerializer.Deserialize<UpdateSettings>(System.IO.File.ReadAllText(FilePath))
                   ?? new UpdateSettings();
        }
        catch
        {
            return new UpdateSettings();
        }
    }

    public static void Save(UpdateSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // best-effort
        }
    }
}
