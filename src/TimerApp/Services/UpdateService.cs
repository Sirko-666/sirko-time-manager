using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;
using TimerApp.Models;

namespace TimerApp.Services;

public enum UpdateStatus
{
    UpToDate,
    Available,
    NoNetwork,
    Error
}

public sealed class UpdateCheckResult
{
    public UpdateStatus Status { get; init; }
    public UpdateInfo? Info { get; init; }
}

/// <summary>
/// Checks GitHub Releases for a newer version and answers environment questions
/// needed by the update flow (.NET runtime presence, installed location).
/// All network calls are best-effort: failures never break the app.
/// </summary>
public static class UpdateService
{
    private const string OwnerRepo = "Sirko-666/sirko-time-manager";
    private const string LatestUrl = $"https://api.github.com/repos/{OwnerRepo}/releases/latest";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\STM";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("STM-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>Current application version (assembly version).</summary>
    public static Version CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    public static string CurrentVersionText
    {
        get
        {
            Version v = CurrentVersion;
            return v.Revision > 0
                ? $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"
                : v.Build > 0
                    ? $"{v.Major}.{v.Minor}.{v.Build}"
                    : $"{v.Major}.{v.Minor}";
        }
    }

    /// <summary>Queries GitHub for the latest release and compares it with the current version.</summary>
    public static async Task<UpdateCheckResult> CheckAsync(string languageKey)
    {
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(LatestUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult { Status = UpdateStatus.Error };

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out JsonElement tagEl)
                ? tagEl.GetString() ?? string.Empty
                : string.Empty;
            string releaseUrl = root.TryGetProperty("html_url", out JsonElement urlEl)
                ? urlEl.GetString() ?? string.Empty
                : string.Empty;
            string body = root.TryGetProperty("body", out JsonElement bodyEl)
                ? bodyEl.GetString() ?? string.Empty
                : string.Empty;

            Version version = ParseVersion(tag);
            if (version <= CurrentVersion)
                return new UpdateCheckResult { Status = UpdateStatus.UpToDate };

            string? mini = null;
            string? fatty = null;
            if (root.TryGetProperty("assets", out JsonElement assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out JsonElement nameEl)
                        ? nameEl.GetString()
                        : null;
                    string? url = asset.TryGetProperty("browser_download_url", out JsonElement dlEl)
                        ? dlEl.GetString()
                        : null;
                    if (name is null || url is null) continue;

                    if (name.Contains("Mini", StringComparison.OrdinalIgnoreCase))
                        mini = url;
                    else if (name.Contains("Fatty", StringComparison.OrdinalIgnoreCase))
                        fatty = url;
                }
            }

            return new UpdateCheckResult
            {
                Status = UpdateStatus.Available,
                Info = new UpdateInfo
                {
                    Tag = tag,
                    Version = version,
                    ReleaseUrl = releaseUrl,
                    Notes = ParseNotes(body, languageKey),
                    MiniUrl = mini,
                    FattyUrl = fatty
                }
            };
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult { Status = UpdateStatus.NoNetwork };
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult { Status = UpdateStatus.NoNetwork };
        }
        catch
        {
            return new UpdateCheckResult { Status = UpdateStatus.Error };
        }
    }

    /// <summary>Downloads a file to the given path. Returns false on any failure.</summary>
    public static async Task<bool> DownloadAsync(string url, string destinationPath)
    {
        try
        {
            using HttpResponseMessage response = await Http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            await using var target = new FileStream(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(target).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Version ParseVersion(string tag)
    {
        string text = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out Version? version) ? version : new Version(0, 0);
    }

    /// <summary>
    /// Release notes support three language sections: [uk]..[/uk], [en]..[/en], [ru]..[/ru].
    /// Falls back to the whole body when no section matches.
    /// </summary>
    private static string ParseNotes(string body, string languageKey)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        string open = $"[{languageKey}]";
        string close = $"[/{languageKey}]";
        int start = body.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return body.Trim();

        start += open.Length;
        int end = body.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        string section = end < 0 ? body[start..] : body[start..end];
        return section.Trim();
    }

    /// <summary>Directory the app is installed into, from the Apps &amp; Features entry.</summary>
    public static string? GetInstalledDir()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            return (key?.GetValue("InstallLocation") as string)?.TrimEnd('\\', '/');
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// True only when the running exe lives in the registered install directory.
    /// Portable / dev copies (bin\Test) return false, so they never auto-update.
    /// </summary>
    public static bool IsRunningFromInstall()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;
            string? dir = Path.GetDirectoryName(exe)?.TrimEnd('\\', '/');
            string? installed = GetInstalledDir();
            return !string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(installed) &&
                   string.Equals(dir, installed, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True when the .NET 8 Desktop Runtime is installed (Mini installer can run).</summary>
    public static bool IsDotNet8DesktopRuntimeInstalled()
    {
        try
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "shared", "Microsoft.WindowsDesktop.App");
            if (Directory.Exists(root) &&
                Directory.EnumerateDirectories(root).Any(
                    d => Path.GetFileName(d).StartsWith("8.", StringComparison.Ordinal)))
                return true;
        }
        catch
        {
            // fall through to the registry probe
        }

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App");
            if (key is not null &&
                key.GetValueNames().Any(n => n.StartsWith("8.", StringComparison.Ordinal)))
                return true;
        }
        catch
        {
            // best-effort
        }

        return false;
    }
}
