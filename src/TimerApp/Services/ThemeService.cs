using System.Windows;

namespace TimerApp.Services;

public enum AppTheme
{
    Dark,
    Light
}

/// <summary>Swaps the active palette dictionary at runtime.</summary>
public static class ThemeService
{
    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    public static void Apply(AppTheme theme)
    {
        Current = theme;
        var merged = Application.Current.Resources.MergedDictionaries;

        for (int i = merged.Count - 1; i >= 0; i--)
        {
            string? src = merged[i].Source?.OriginalString;
            if (src is not null && (src.Contains("ThemeDark") || src.Contains("ThemeLight")))
                merged.RemoveAt(i);
        }

        string file = theme == AppTheme.Dark ? "ThemeDark" : "ThemeLight";
        merged.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Themes/{file}.xaml", UriKind.Relative)
        });

        ThemeChanged?.Invoke(theme);
    }

    public static string ToKey(AppTheme theme) => theme == AppTheme.Dark ? "dark" : "light";

    public static AppTheme FromKey(string? key) => key == "light" ? AppTheme.Light : AppTheme.Dark;
}