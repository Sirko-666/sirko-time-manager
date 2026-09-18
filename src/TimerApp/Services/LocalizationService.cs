using System.Windows;

namespace TimerApp.Services;

public enum AppLanguage
{
    Ukrainian,
    English,
    Russian
}

/// <summary>Swaps the active string dictionary at runtime.</summary>
public static class LocalizationService
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Ukrainian;

    public static event Action<AppLanguage>? LanguageChanged;

    public static void Apply(AppLanguage language)
    {
        Current = language;
        var merged = Application.Current.Resources.MergedDictionaries;

        for (int i = merged.Count - 1; i >= 0; i--)
        {
            string? src = merged[i].Source?.OriginalString;
            if (src is not null && src.Contains("Strings."))
                merged.RemoveAt(i);
        }

        string file = language switch
        {
            AppLanguage.English => "Strings.en",
            AppLanguage.Russian => "Strings.ru",
            _ => "Strings.uk"
        };

        merged.Add(new ResourceDictionary
        {
            Source = new Uri($"Localization/{file}.xaml", UriKind.Relative)
        });

        LanguageChanged?.Invoke(language);
    }

    public static string Get(string key) =>
        Application.Current.TryFindResource(key) as string ?? key;

    public static string ToKey(AppLanguage language) => language switch
    {
        AppLanguage.English => "en",
        AppLanguage.Russian => "ru",
        _ => "uk"
    };

    public static AppLanguage FromKey(string? key) => key switch
    {
        "en" => AppLanguage.English,
        "ru" => AppLanguage.Russian,
        _ => AppLanguage.Ukrainian
    };
}