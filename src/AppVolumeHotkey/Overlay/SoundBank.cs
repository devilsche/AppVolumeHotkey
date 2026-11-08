using System.Media;
using System.Windows;
using AppVolumeHotkey.Core.Model;

namespace AppVolumeHotkey.Overlay;

/// <summary>Eigene Bestätigungstöne (eingebettete WAV-Dateien), je Variante ein Ton für „aktiv“ und „aus“.</summary>
public static class SoundBank
{
    private static readonly Dictionary<string, SoundPlayer> Cache = new();

    public static IReadOnlyList<(SoundVariant Variant, string Label)> Variants { get; } = new[]
    {
        (SoundVariant.TwoTones, "Zwei Töne"),
        (SoundVariant.Click, "Klick"),
        (SoundVariant.Bell, "Glocke"),
        (SoundVariant.Blip, "Blip"),
        (SoundVariant.Soft, "Sanft"),
    };

    public static void Play(SoundVariant variant, bool active)
    {
        var name = $"{variant}-{(active ? "on" : "off")}";
        try
        {
            if (!Cache.TryGetValue(name, out var player))
            {
                var info = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Sounds/{name}.wav"));
                if (info is null)
                    return;
                player = new SoundPlayer(info.Stream);
                player.Load();
                Cache[name] = player;
            }
            player.Play(); // asynchron
        }
        catch (Exception)
        {
            SystemSounds.Beep.Play();
        }
    }
}
