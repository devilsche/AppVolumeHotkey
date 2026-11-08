using System.Reflection;
using System.IO;

namespace AppVolumeHotkey.Platform;

public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/devilsche/AppVolumeHotkey";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
        ?? "?";

    public static string Copyright { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    public static string ThirdPartyNotices()
    {
        using var s = typeof(AppInfo).Assembly.GetManifestResourceStream("THIRD-PARTY-NOTICES.txt");
        if (s is null)
            return "";
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
