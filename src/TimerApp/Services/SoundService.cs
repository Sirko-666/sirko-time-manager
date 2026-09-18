using System.IO;
using System.Media;
using System.Windows.Media;
using System.Windows.Threading;
using TimerApp.Models;

namespace TimerApp.Services;

/// <summary>
/// Catalog and playback of alarm sounds.
/// A key is one of:
///   "sys:&lt;Name&gt;"   — a built-in Windows sound,
///   "file:&lt;name&gt;"  — a wave file from %WinDir%\Media,
///   "user:&lt;name&gt;"  — a user-imported wav/mp3 in %AppData%\TimerApp\Sounds.
/// </summary>
public static class SoundService
{
    public const string DefaultKey = Alarm.DefaultSoundKey;

    private static readonly string[] SystemNames =
        ["Asterisk", "Beep", "Exclamation", "Hand", "Question"];

    private static readonly string MediaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

    public static readonly string UserDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimerApp", "Sounds");

    private static MediaPlayer? _media;
    private static SoundPlayer? _player;
    private static bool _looping;

    public static IReadOnlyList<string> All()
    {
        var keys = new List<string>();
        keys.AddRange(UserKeys());
        keys.AddRange(SystemNames.Select(n => "sys:" + n));
        keys.AddRange(WindowsKeys());
        return keys;
    }

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

    private static IEnumerable<string> WindowsKeys()
    {
        try
        {
            if (!Directory.Exists(MediaDir)) return [];
            return Directory.EnumerateFiles(MediaDir, "*.wav")
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => "file:" + n);
        }
        catch
        {
            return [];
        }
    }

    private static bool IsSupportedFile(string path) =>
        path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    public static bool IsFileSound(string key) =>
        key.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith("user:", StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string key)
    {
        if (key.StartsWith("sys:", StringComparison.OrdinalIgnoreCase)) return key[4..];
        if (IsFileSound(key)) return Path.GetFileNameWithoutExtension(key[5..]);
        return key;
    }

    private static string? FilePathOf(string key)
    {
        if (!IsFileSound(key)) return null;
        string name = key[5..];
        string dir = key.StartsWith("user:", StringComparison.OrdinalIgnoreCase) ? UserDir : MediaDir;
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
            string name = Path.GetFileName(key[5..]);
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
        key = string.IsNullOrEmpty(key) ? DefaultKey : key;

        if (!IsFileSound(key))
        {
            StopFilePlayback();
            switch (key.StartsWith("sys:", StringComparison.OrdinalIgnoreCase) ? key[4..] : "")
            {
                case "Asterisk": SystemSounds.Asterisk.Play(); break;
                case "Beep": SystemSounds.Beep.Play(); break;
                case "Hand": SystemSounds.Hand.Play(); break;
                case "Question": SystemSounds.Question.Play(); break;
                default: SystemSounds.Exclamation.Play(); break;
            }
            return;
        }

        string? path = FilePathOf(key);
        if (path is null)
        {
            Play(DefaultKey);
            return;
        }

        StartMedia(path, loop: false);
    }

    /// <summary>Plays a file sound in a seamless loop until Stop()/StopLooping().</summary>
    public static void PlayLooping(string? key)
    {
        key = string.IsNullOrEmpty(key) ? DefaultKey : key;

        if (!IsFileSound(key))
        {
            Play(key);
            return;
        }

        string? path = FilePathOf(key);
        if (path is null) return;

        StartMedia(path, loop: true);
    }

    public static void StopLooping() => StopFilePlayback();

    public static void Stop() => StopFilePlayback();

    /// <summary>
    /// Resolves the duration of a file sound (probe media open on the UI thread).
    /// Returns null for system sounds, missing files and failures.
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
        if (_media is not null)
        {
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

        if (_player is not null)
        {
            try
            {
                _player.Stop();
            }
            catch
            {
                // Ignore.
            }
            _player = null;
        }
    }
}
