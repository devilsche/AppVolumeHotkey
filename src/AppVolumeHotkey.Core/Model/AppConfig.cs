namespace AppVolumeHotkey.Core.Model;

public sealed class AppConfig
{
    public List<AppGroup> Groups { get; set; } = new();
    public AppSettings Settings { get; set; } = new();

    public IEnumerable<(AppGroup Group, Profile Profile)> AllProfiles() =>
        Groups.SelectMany(g => g.Profiles.Select(p => (g, p)));
}
