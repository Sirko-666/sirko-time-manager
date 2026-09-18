using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StmInstaller.Services;

namespace StmInstaller;

public partial class InstallerWindow : Window
{
    private int _step;
    private readonly DispatcherTimer _installTimer;
    private Task? _installTask;
    private string? _installError;
    private bool _langInitializing;

    private string _targetDir = string.Empty;
    private bool _desktopShortcut;
    private bool _startMenuShortcut;
    private bool _autostart;

    public InstallerWindow()
    {
        InitializeComponent();

        _installTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _installTimer.Tick += OnInstallTick;

        UiLanguage.Apply(InstallerLanguage.Ukrainian);
        UiLanguage.LanguageChanged += ApplyTexts;
        _langInitializing = true;
        LangUk.IsChecked = true;
        _langInitializing = false;

        PathBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Sirko Time Manager");

        MouseLeftButtonDown += (_, _) => DragMove();
        ApplyTexts();
        UpdateStep();
    }

    private static string T(string key) =>
        (string?)Application.Current.TryFindResource(key) ?? key;

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        if (_langInitializing) return;

        var language = sender == LangEn ? InstallerLanguage.English
            : sender == LangRu ? InstallerLanguage.Russian
            : InstallerLanguage.Ukrainian;
        UiLanguage.Apply(language);
    }

    private void ApplyTexts()
    {
        // Everything static in XAML follows DynamicResource automatically;
        // the step pills carry a numeric prefix, so they are set here.
        Step1Text.Text = $"1 · {T("L_Step1")}";
        Step2Text.Text = $"2 · {T("L_Step2")}";
        Step3Text.Text = $"3 · {T("L_Step3")}";
        Step4Text.Text = $"4 · {T("L_Step4")}";
        Title = T("L_InstallerTitle");
    }

    // -------- Install engine --------

    private const string ExtractDirName = "STM-Setup";

    /// <summary>Resolves the app payload: local "app" folder, embedded pack, or dev build outputs.</summary>
    private static string EnsureAppPayload()
    {
        string baseDir = AppContext.BaseDirectory;

        // Bundled layout: "app" folder next to the installer (dev mode).
        string local = Path.Combine(baseDir, "app");
        if (Directory.Exists(local) && File.Exists(Path.Combine(local, "TimerApp.exe")))
            return local;

        // Self-extracting: unpack the embedded package into %TEMP%\STM-Setup\app.
        if (Directory.Exists(Path.Combine(Path.GetTempPath(), ExtractDirName, "app")) &&
            File.Exists(Path.Combine(Path.GetTempPath(), ExtractDirName, "app", "TimerApp.exe")))
            return Path.Combine(Path.GetTempPath(), ExtractDirName, "app");

        string tempDir = Path.Combine(Path.GetTempPath(), ExtractDirName, "app");
        try
        {
            using Stream? packStream = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("StmInstaller.pack.zip");
            if (packStream is not null)
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
                Directory.CreateDirectory(tempDir);

                using var archive = new System.IO.Compression.ZipArchive(packStream);
                foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
                {
                    string entryPath = entry.FullName.Replace('\\', Path.DirectorySeparatorChar);
                    string path = Path.Combine(tempDir, entryPath);
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(path);
                        continue;
                    }

                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    using Stream source = entry.Open();
                    using FileStream target = File.Create(path);
                    source.CopyTo(target);
                }
                return tempDir;
            }
        }
        catch
        {
            // Embedded pack unavailable/failed: fall back to dev outputs below.
        }

        var devCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\TimerApp\bin\Release\net8.0-windows")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\TimerApp\bin\Debug\net8.0-windows"))
        };
        foreach (string candidate in devCandidates)
        {
            if (File.Exists(Path.Combine(candidate, "TimerApp.exe")))
                return candidate;
        }

        return devCandidates[^1];
    }

    private static string FindAppSource() => EnsureAppPayload();

    private void PersistChosenLanguage()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimerApp");
        Directory.CreateDirectory(dir);
        string key = UiLanguage.ToKey(UiLanguage.Current);
        File.WriteAllText(Path.Combine(dir, "settings.json"),
            System.Text.Json.JsonSerializer.Serialize(
                new { Theme = "dark", Language = key }));
    }

    private void DoInstall()
    {
        try
        {
            Directory.CreateDirectory(_targetDir);
            PersistChosenLanguage();

            string source = FindAppSource();
            int copied = 0;
            foreach (string file in Directory.EnumerateFiles(source))
            {
                string name = Path.GetFileName(file);
                string extension = Path.GetExtension(name);
                if (name is null) continue;
                if (extension is ".pdb" or ".xml") continue;
                File.Copy(file, Path.Combine(_targetDir, name), overwrite: true);
                copied++;
            }

            string exe = Path.Combine(_targetDir, "TimerApp.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException(T("L_NoExe"));

            if (_desktopShortcut)
            {
                ShortcutService.Create(exe,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        "STM — Sirko Time Manager.lnk"),
                    "Sirko Time Manager");
            }

            if (_startMenuShortcut)
            {
                ShortcutService.Create(exe,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs", "STM — Sirko Time Manager.lnk"),
                    "Sirko Time Manager");
            }

            if (_autostart)
            {
                Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");
                key?.SetValue("TimerApp", $"\"{exe}\" --autostart", Microsoft.Win32.RegistryValueKind.String);
            }
        }
        catch (Exception ex)
        {
            _installError = ex.Message;
        }
    }

    private void ReadOptions()
    {
        _targetDir = PathBox.Text.Trim();
        _desktopShortcut = OptDesktopSwitch.IsChecked == true;
        _startMenuShortcut = OptStartMenuSwitch.IsChecked == true;
        _autostart = OptAutostartSwitch.IsChecked == true;
    }

    private string[] InstallSteps() =>
    [
        T("L_LogCopy"),
        T("L_LogDesktop"),
        T("L_LogStartMenu"),
        T("L_LogAutostart"),
        T("L_LogFinish")
    ];

    private void OnInstallTick(object? sender, EventArgs e)
    {
        double value = InstallBar.Value + 7;

        if (value >= 100)
        {
            // Wait for the real file work before showing the final page.
            if (_installTask is null || !_installTask.IsCompleted)
            {
                InstallBar.Value = 99;
                InstallLog.Text = T("L_WaitFiles");
                return;
            }

            _installTimer.Stop();
            InstallBar.Value = 100;
            InstallLog.Text = _installError is null
                ? T("L_InstallOk")
                : string.Format(T("L_InstallFailed"), _installError);
            _step = 4;
            UpdateStep();
            return;
        }

        InstallBar.Value = value;
        string[] steps = InstallSteps();
        int index = (int)Math.Min(value / 110, steps.Length - 1);
        InstallLog.Text = steps[index];
    }

    // ---------- Wizard ----------

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
                _step = 1;
                break;

            case 1:
                _step = 2;
                break;

            case 2:
                ReadOptions();
                if (string.IsNullOrWhiteSpace(_targetDir))
                {
                    InstallLog.Visibility = System.Windows.Visibility.Visible;
                    InstallLog.Text = T("L_InvalidDir");
                    return;
                }
                _step = 3;
                InstallBar.Value = 0;
                InstallLog.Text = T("L_LogCopy");
                BackButton.Visibility = System.Windows.Visibility.Hidden;
                NextButton.IsEnabled = false;
                NextButton.Visibility = System.Windows.Visibility.Hidden;
                _installTask = Task.Run(DoInstall);
                _installTimer.Start();
                return;

            case 4:
                if (_installError is null && OptRunNowSwitch.IsChecked == true)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(
                            Path.Combine(_targetDir, "TimerApp.exe")) { UseShellExecute = true });
                    }
                    catch
                    {
                        // Best-effort launch.
                    }
                }
                Close();
                return;

            default:
                return;
        }

        UpdateStep();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_step is < 1 or >= 4) return;
        _step--;
        UpdateStep();
    }

    private void UpdateStep()
    {
        PageWelcome.Visibility = _step == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PagePath.Visibility = _step == 1 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PageOptions.Visibility = _step == 2 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PageInstall.Visibility = _step == 3 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PageDone.Visibility = _step == 4 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        NextButton.Content = _step switch
        {
            0 => T("L_InstallerStart"),
            1 => T("L_Next"),
            2 => T("L_InstallNow"),
            4 => T("L_InstallerFinish"),
            _ => T("L_Next")
        };
        NextButton.IsEnabled = _step != 3;
        NextButton.Visibility = _step == 3 ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
        BackButton.Visibility = _step is 1 or 2 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;

        UpdatePill(Step1Pill, Step1Text, _step == 0);
        UpdatePill(Step2Pill, Step2Text, _step == 1);
        UpdatePill(Step3Pill, Step3Text, _step == 2);
        UpdatePill(Step4Pill, Step4Text, _step is 3 or 4);
    }

    private static void UpdatePill(Border pill, TextBlock text, bool active)
    {
        pill.Background = active
            ? (Brush)Application.Current.FindResource("InkBrush")
            : Brushes.Transparent;
        pill.BorderBrush = (Brush)Application.Current.FindResource(
            active ? "InkBrush" : "HairlineBrush");
        text.Foreground = (Brush)Application.Current.FindResource(
            active ? "OnInkBrush" : "MutedTextBrush");
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = T("L_BrowseTitle"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        if (dialog.ShowDialog(this) == true)
            PathBox.Text = dialog.FolderName;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
