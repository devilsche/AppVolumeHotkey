using System.Text.RegularExpressions;
using AppVolumeHotkey.Core.Localization;
using AppVolumeHotkey.Core.Model;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // Sprache ist globaler Zustand

namespace AppVolumeHotkey.Core.Tests;

public sealed class LocalizationTests : IDisposable
{
    public void Dispose() => L.SetLanguage("de");

    [Fact]
    public void BothLanguages_HaveSameKeys_AndNoEmptyValues()
    {
        Assert.Equal(Strings.De.Keys.OrderBy(k => k), Strings.En.Keys.OrderBy(k => k));
        Assert.All(Strings.De.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
        Assert.All(Strings.En.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public void Placeholders_MatchBetweenLanguages()
    {
        static string Ph(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().OrderBy(x => x));
        foreach (var key in Strings.De.Keys)
            Assert.True(Ph(Strings.De[key]) == Ph(Strings.En[key]), $"Platzhalter unterschiedlich bei {key}");
    }

    [Fact]
    public void Switching_ChangesTexts_AndRaisesEvent()
    {
        var raised = 0;
        L.Changed += Handler;
        try
        {
            L.SetLanguage("en");
            Assert.Equal("original volume", Step.Baseline.Label);
            Assert.Equal("20 % of original volume → original volume",
                new Profile { Mode = ValueMode.Relative, Steps = { Step.Percent(20), Step.Baseline } }.StepsSummary);
            L.SetLanguage("de");
            Assert.Equal("Ausgangswert", Step.Baseline.Label);
            Assert.Equal(2, raised);
        }
        finally
        {
            L.Changed -= Handler;
        }

        void Handler() => raised++;
    }

    [Theory]
    [InlineData("de", "de-DE", "de")]
    [InlineData("en", "de-DE", "en")]
    [InlineData("auto", "de-AT", "de")]
    [InlineData("auto", "fr-FR", "en")]
    [InlineData(null, "de-DE", "de")]
    [InlineData("xx", "de-DE", "de")]
    public void Resolve(string? setting, string uiCulture, string expected) =>
        Assert.Equal(expected, L.Resolve(setting, new System.Globalization.CultureInfo(uiCulture)));

    [Fact]
    public void MissingKey_ReturnsKey() => Assert.Equal("Does_Not_Exist", L.T("Does_Not_Exist"));

    [Fact]
    public void LanguageSetting_DefaultsToAuto() => Assert.Equal("auto", new AppSettings().Language);
}

public sealed class AccessKeyTests
{
    // Texte mit Alt-Kürzel (_X), die gleichzeitig in einem Fenster bzw. einer Einstellungsseite sichtbar sind
    public static TheoryData<string, string[]> Windows => new()
    {
        { "Hauptfenster", new[] { "Main_Settings", "Main_About", "Main_Groups", "Main_NewGroup", "Main_AddExe", "Main_Profiles", "Main_NewProfile", "Main_Running", "Main_AddToGroup" } },
        { "Profil-Dialog", new[] { "Profile_Name", "Profile_Hotkey", "Profile_PassThrough", "Profile_TriggerToggle", "Profile_TriggerHold", "Profile_ModeRelative", "Profile_ModeAbsolute", "Profile_Sequence", "Profile_AddPercent", "Profile_AddBaseline", "Profile_OverlayOn", "Profile_OverlayOff", "Profile_Sound", "Profile_SoundLabel", "Common_Save" } },
        { "Einstellungen/Allgemein", new[] { "Settings_Language", "Settings_StartWithWindows", "Settings_StartMinimized", "Settings_MinimizeToTray", "Settings_RunAsAdmin", "Settings_Fade", "Common_Save" } },
        { "Einstellungen/Overlay", new[] { "Settings_Position", "Settings_Size", "Settings_Duration", "Settings_TestOverlay", "Common_Save" } },
        { "Hauptfenster/Mikrofon (an)", new[] { "Main_Settings", "Main_About", "Main_Groups", "Main_NewGroup", "Main_Running", "Main_AddToGroup",
            "Mic_TurnOff", "Mic_Hotkey", "Mic_ModeToggle", "Mic_ModeHold", "Mic_ModePtt", "Mic_ScopeAll", "Mic_ScopeDefault", "Mic_ShowOverlay",
            "Mic_PlaySound", "Mic_SoundLabel", "Mic_ShowInStatus", "Mic_UnmuteOnExit", "Mic_PassThrough" } },
        { "Hauptfenster/Mikrofon (stumm)", new[] { "Main_Settings", "Main_About", "Main_Groups", "Main_NewGroup", "Main_Running", "Main_AddToGroup",
            "Mic_TurnOn", "Mic_Hotkey", "Mic_ModeToggle", "Mic_ModeHold", "Mic_ModePtt", "Mic_ScopeAll", "Mic_ScopeDefault", "Mic_ShowOverlay",
            "Mic_PlaySound", "Mic_SoundLabel", "Mic_ShowInStatus", "Mic_UnmuteOnExit", "Mic_PassThrough" } },
        { "Hauptfenster/Ducking", new[] { "Main_Settings", "Main_About", "Main_Groups", "Main_NewGroup", "Main_Running", "Main_AddToGroup",
            "Ducking_Enabled", "Ducking_ToggleHotkey", "Ducking_Source", "Ducking_TargetAll", "Ducking_TargetGroups", "Ducking_Percent",
            "Ducking_Threshold", "Ducking_Release", "Ducking_AddRule" } },
        { "Infobereich-Menü (Mikrofon/Ducking)", new[] { "Tray_ResetAll", "Tray_Pause", "Tray_Open", "Tray_About", "Tray_Exit", "Tray_MicMute", "Tray_Ducking" } },
        { "Infobereich-Menü (Mikrofon stumm)", new[] { "Tray_ResetAll", "Tray_Pause", "Tray_Open", "Tray_About", "Tray_Exit", "Tray_MicUnmute", "Tray_Ducking" } },
        { "Einstellungen/Statusanzeige", new[] { "Settings_StatusOn", "Settings_StatusStyle", "Settings_StatusCorner", "Settings_StatusOpacity", "Settings_TrayBadge", "Common_Save" } },
        { "Infobereich-Menü", new[] { "Tray_ResetAll", "Tray_Pause", "Tray_Open", "Tray_About", "Tray_Exit" } },
        { "Über", new[] { "About_OpenData", "Common_Close" } },
    };

    [Theory]
    [MemberData(nameof(Windows))]
    public void AccessKeys_AreUniquePerWindow(string window, string[] keys)
    {
        foreach (var table in new[] { Strings.De, Strings.En })
        {
            var letters = keys.Select(k => (Key: k, Letter: AccessLetter(table[k]))).ToList();
            Assert.All(letters, l => Assert.True(l.Letter is not null, $"{window}: {l.Key} hat kein Alt-Kürzel"));
            var dupes = letters.GroupBy(l => l.Letter).Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Key))}").ToList();
            Assert.True(dupes.Count == 0, $"{window}: doppelte Alt-Kürzel – {string.Join("; ", dupes)}");
        }
    }

    private static char? AccessLetter(string text)
    {
        var i = text.IndexOf('_');
        return i >= 0 && i + 1 < text.Length ? char.ToLowerInvariant(text[i + 1]) : null;
    }
}
