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
