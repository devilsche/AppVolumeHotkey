namespace AppVolumeHotkey.Core.Model;

public sealed class AppGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Neue Gruppe";

    /// <summary>Namen der .exe-Dateien, z. B. „Discord.exe“ (Vergleich ohne Groß-/Kleinschreibung).</summary>
    public List<string> Apps { get; set; } = new();

    public List<Profile> Profiles { get; set; } = new();

    public bool ContainsApp(string exe) => Apps.Contains(exe, StringComparer.OrdinalIgnoreCase);
}
