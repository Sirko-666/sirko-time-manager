using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;
using TimerApp.Models;

namespace TimerApp.Services;

/// <summary>
/// Catalog and playback of alarm sounds.
/// A key is one of:
///   "app:&lt;name&gt;"    — a built-in STM sound (embedded, extracted to %AppData%\TimerApp\Sounds\BuiltIn),
///   "user:&lt;name&gt;"   — a user-imported wav/mp3 in %AppData%\TimerApp\Sounds.
/// Any other (legacy) key falls back to the default built-in sound.
/// </summary>
public static class SoundService
{
    public const string DefaultKey = Alarm.DefaultSoundKey;

    private const string ResourcePrefix = "TimerApp.Assets.Sounds.";

    public static readonly string UserDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimerApp", "Sounds");

    private static readonly string BuiltInDir = Path.Combine(UserDir, "BuiltIn");

    private static MediaPlayer? _media;
    private static bool _looping;

    private static double _volume = -1;

    /// <summary>Alarm playback volume (0..1), relative to the Windows system volume.</summary>
    public static double Volume
    {
        get
        {
            if (_volume < 0) _volume = SettingsStore.LoadVolume();
            return _volume;
        }
        set
        {
            double v = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(v - _volume) < 0.0005) return;
            _volume = v;
            SettingsStore.SaveVolume(v);
            if (_media is not null)
            {
                try { _media.Volume = v; } catch { /* ignore */ }
            }
            VolumeChanged?.Invoke(v);
        }
    }

    /// <summary>Raised when Volume changes, so all sliders can stay in sync.</summary>
    public static event Action<double>? VolumeChanged;

    public static IReadOnlyList<string> All()
    {
        var keys = new List<string>();
        keys.AddRange(BuiltInKeys());
        keys.AddRange(UserKeys());
        return keys;
    }

    /// <summary>Keys of the embedded built-in sounds (also extracts them on demand).</summary>
    private static IEnumerable<string> BuiltInKeys()
    {
        try
        {
            EnsureBuiltIn();
            return Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .Where(r => r.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                .Select(r => r[ResourcePrefix.Length..])
                .Where(IsSupportedName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => "app:" + n);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Extracts embedded built-in sounds into %AppData%\TimerApp\Sounds\BuiltIn (missing/changed only).</summary>
    private static void EnsureBuiltIn()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] resources = assembly.GetManifestResourceNames()
                .Where(r => r.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                .ToArray();
            if (resources.Length == 0) return;

            Directory.CreateDirectory(BuiltInDir);
            foreach (string resource in resources)
            {
                string name = resource[ResourcePrefix.Length..];
                if (!IsSupportedName(name)) continue;

                string target = Path.Combine(BuiltInDir, name);
                using Stream? source = assembly.GetManifestResourceStream(resource);
                if (source is null) continue;

                // Re-extract when the file is missing or the embedded content changed.
                if (File.Exists(target) && new FileInfo(target).Length == source.Length)
                    continue;

                using FileStream file = File.Create(target);
                source.CopyTo(file);
            }
        }
        catch
        {
            // Best-effort; a missing built-in sound falls back to the default.
        }
    }

    private static bool IsSupportedName(string name) =>
        name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> UserKeys()
    {
        try
        {
            if (!Directory.Exists(UserDir)) return [];
            return Directory.EnumerateFiles(UserDir)
                .Where(IsSupportedFile)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => "user:" + n);
        }
        catch
        {
            return [];
        }
    }

    private static bool IsSupportedFile(string path) =>
        path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for built-in and user file sounds.</summary>
    public static bool IsFileSound(string key) =>
        key.StartsWith("app:", StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith("user:", StringComparison.OrdinalIgnoreCase);

    public static bool IsBuiltIn(string key) =>
        key.StartsWith("app:", StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string key) =>
        IsFileSound(key) ? Path.GetFileNameWithoutExtension(NameOf(key)) : key;

    /// <summary>File name part of a file-sound key. "app:" has 4 chars, "user:" has 5.</summary>
    private static string NameOf(string key)
    {
        if (key.StartsWith("app:", StringComparison.OrdinalIgnoreCase)) return key[4..];
        if (key.StartsWith("user:", StringComparison.OrdinalIgnoreCase)) return key[5..];
        return key;
    }

    private static string? FilePathOf(string key)
    {
        if (!IsFileSound(key)) return null;
        string name = NameOf(key);
        string dir = key.StartsWith("app:", StringComparison.OrdinalIgnoreCase) ? BuiltInDir : UserDir;
        if (key.StartsWith("app:", StringComparison.OrdinalIgnoreCase)) EnsureBuiltIn();

        string path = Path.Combine(dir, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Copies a sound file into the user folder. Returns its new key, or null on failure.</summary>
    public static string? AddUserSound(string sourcePath)
    {
        try
        {
            if (!IsSupportedFile(sourcePath) || !File.Exists(sourcePath)) return null;

            Directory.CreateDirectory(UserDir);
            string name = Path.GetFileName(sourcePath);
            string target = Path.Combine(UserDir, name);

            int counter = 1;
            while (File.Exists(target) &&
                   !string.Equals(Path.GetFullPath(target), Path.GetFullPath(sourcePath),
                       StringComparison.OrdinalIgnoreCase))
            {
                target = Path.Combine(UserDir,
                    $"{Path.GetFileNameWithoutExtension(name)} ({counter++}){Path.GetExtension(name)}");
            }

            if (!File.Exists(target)) File.Copy(sourcePath, target);
            return "user:" + Path.GetFileName(target);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Removes a user-imported sound by its key. Returns true on success.</summary>
    public static bool DeleteUserSound(string key)
    {
        if (!key.StartsWith("user:", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            string name = Path.GetFileName(NameOf(key));
            string path = Path.Combine(UserDir, name);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Play(string? key)
    {
        if (string.IsNullOrEmpty(key) || !IsFileSound(key))
            key = DefaultKey;

        string? path = FilePathOf(key);
        if (path is null)
        {
            // Fall back to the default sound; if it is unavailable, do nothing
            // (there are no system sounds anymore).
            if (!string.Equals(key, DefaultKey, StringComparison.OrdinalIgnoreCase))
                Play(DefaultKey);
            return;
        }

        StartMedia(path, loop: false);
    }

    /// <summary>Plays a file sound in a seamless loop until Stop()/StopLooping().</summary>
    public static void PlayLooping(string? key)
    {
        if (string.IsNullOrEmpty(key) || !IsFileSound(key))
            key = DefaultKey;

        string? path = FilePathOf(key);
        if (path is null) return;

        StartMedia(path, loop: true);
    }

    public static void StopLooping() => StopFilePlayback();

    public static void Stop() => StopFilePlayback();

    /// <summary>
    /// Resolves the duration of a file sound (probe media open on the UI thread).
    /// Returns null for missing files and failures.
    /// </summary>
    public static Task<TimeSpan?> GetDurationAsync(string key)
    {
        string? path = FilePathOf(key);
        if (path is null) return Task.FromResult<TimeSpan?>(null);

        var completion = new TaskCompletionSource<TimeSpan?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        bool finished = false;

        void Finish(TimeSpan? duration)
        {
            if (finished) return;
            finished = true;
            timeout.Stop();
            CloseQuiet(player);
            completion.TrySetResult(duration);
        }

        player.MediaOpened += (_, _) => Finish(
            player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan : null);
        player.MediaFailed += (_, _) => Finish(null);
        timeout.Tick += (_, _) => Finish(null);

        try
        {
            player.Open(new Uri(path));
            timeout.Start();
        }
        catch
        {
            Finish(null);
        }

        return completion.Task;
    }

    private static void CloseQuiet(MediaPlayer player)
    {
        try
        {
            player.Stop();
            player.Close();
        }
        catch
        {
            // Ignore.
        }
    }

    private static void StartMedia(string path, bool loop)
    {
        StopFilePlayback();
        try
        {
            _media = new MediaPlayer();
            _media.Open(new Uri(path));
            _media.Volume = Volume;
            if (loop)
            {
                _looping = true;
                _media.MediaEnded += OnMediaEnded;
            }
            _media.Play();
        }
        catch
        {
            _media = null;
            _looping = false;
        }
    }

    private static void OnMediaEnded(object? sender, EventArgs e)
    {
        if (!_looping || _media is null) return;
        _media.Position = TimeSpan.Zero;
        _media.Play();
    }

    private static void StopFilePlayback()
    {
        _looping = false;
        if (_media is null) return;
        try
        {
            _media.MediaEnded -= OnMediaEnded;
            _media.Stop();
            _media.Close();
        }
        catch
        {
            // Ignore.
        }
        _media = null;
    }
}
