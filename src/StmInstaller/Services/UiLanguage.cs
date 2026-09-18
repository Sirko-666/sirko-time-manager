using System.Windows;

namespace StmInstaller.Services;

public enum InstallerLanguage
{
    Ukrainian,
    English,
    Russian
}

internal static class UiLanguage
{
    public static InstallerLanguage Current { get; private set; } = InstallerLanguage.Ukrainian;

    public static event Action? LanguageChanged;

    public static string ToKey(InstallerLanguage language) => language switch
    {
        InstallerLanguage.English => "en",
        InstallerLanguage.Russian => "ru",
        _ => "uk"
    };

    public static void Apply(InstallerLanguage language)
    {
        if (language == Current && Application.Current.Resources.MergedDictionaries
                .Any(d => d.Source?.OriginalString.Contains("Strings.") == true)) return;

        Current = language;
        var merged = Application.Current.Resources.MergedDictionaries;

        var old = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Strings.") == true);
        int index = old is null ? merged.Count : merged.IndexOf(old);
        if (old is not null) merged.Remove(old);

        var strings = new ResourceDictionary
        {
            Source = new Uri($"/Localization/Strings.{ToKey(language)}.xaml", UriKind.Relative)
        };
        merged.Insert(index, strings);

        LanguageChanged?.Invoke();
    }

    public static InstallerLanguage FromKeyProperty(string key) => key switch
    {
        "en" => InstallerLanguage.English,
        "ru" => InstallerLanguage.Russian,
        _ => InstallerLanguage.Ukrainian
    };
}
