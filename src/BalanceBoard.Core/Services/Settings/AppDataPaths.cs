namespace BalanceBoard.Core.Services;

/// <summary>
/// All user data lives under %AppData%\BalanceBoardApp\ (never in the repo).
/// </summary>
public static class AppDataPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BalanceBoardApp");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string LogsDirectory => Path.Combine(Root, "logs");

    public static string ProfilesDirectory => Path.Combine(Root, "profiles");

    public static string ArtifactsDirectory => Path.Combine(Root, "artifacts");

    public static string PhysicalTestsDirectory => Path.Combine(ArtifactsDirectory, "physical-tests");

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
