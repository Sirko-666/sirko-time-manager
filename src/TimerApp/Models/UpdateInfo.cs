namespace TimerApp.Models;

/// <summary>An update discovered on GitHub Releases.</summary>
public sealed class UpdateInfo
{
    /// <summary>Release tag as published, e.g. "v0.1.2.2".</summary>
    public required string Tag { get; init; }

    /// <summary>Parsed numeric version of the tag.</summary>
    public required Version Version { get; init; }

    /// <summary>Human page of the release (opened in the browser as a fallback).</summary>
    public required string ReleaseUrl { get; init; }

    /// <summary>Release notes for the current UI language (already section-parsed).</summary>
    public required string Notes { get; init; }

    /// <summary>Download URL of STM-Setup-Mini.exe (framework-dependent), if present.</summary>
    public string? MiniUrl { get; init; }

    /// <summary>Download URL of STM-Setup-Fatty.exe (self-contained), if present.</summary>
    public string? FattyUrl { get; init; }
}
