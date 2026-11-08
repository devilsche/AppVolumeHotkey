using System.Globalization;

namespace AppVolumeHotkey.Core.Localization;

/// <summary>Aktuelle UI-Sprache und Text-Lookup. Umschalten löst <see cref="Changed"/> aus.</summary>
public static class L
{
    private static IReadOnlyDictionary<string, string> _current = Strings.De;

    /// <summary>„de“ oder „en“.</summary>
    public static string Language { get; private set; } = "de";

    public static event Action? Changed;

    /// <summary>Einstellung („auto“, „de“, „en“) in eine konkrete Sprache auflösen.</summary>
    public static string Resolve(string? setting, CultureInfo? uiCulture = null) => setting switch
    {
        "de" => "de",
        "en" => "en",
        "auto" => (uiCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "de" ? "de" : "en",
        _ => "de",
    };

    public static void SetLanguage(string language)
    {
        var lang = language == "en" ? "en" : "de";
        Language = lang;
        _current = lang == "en" ? Strings.En : Strings.De;
        Changed?.Invoke();
    }

    public static string T(string key) => _current.TryGetValue(key, out var v) ? v : key;

    public static string F(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
}
