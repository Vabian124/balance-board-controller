using System.Reflection;

namespace BalanceBoard.Core.Services.Diagnostics;

public static class AppVersion
{
    private static readonly Lazy<string> VersionLazy = new(() =>
    {
        var asm = typeof(AppVersion).Assembly;
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString(3)
            ?? "0.0.0";
    });

    public const string StabilityLabel = "stable";

    public static string Version => VersionLazy.Value;

    public static string DisplayName => $"Balance Board Controller v{Version} ({StabilityLabel})";

    public static string SessionBanner =>
        $"Balance Board Controller v{Version} ({StabilityLabel}) — reference connect release";

    public static string WindowTitle => DisplayName;
}
