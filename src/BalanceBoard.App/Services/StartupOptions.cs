namespace BalanceBoard.App.Services;

public sealed class StartupOptions
{
    public bool SkipProcessCleanup { get; init; }
    public bool SkipSingleInstance { get; init; }
    public bool ConnectOnLaunch { get; init; }

    public static StartupOptions Parse(string[] args)
    {
        var devMode = string.Equals(
            Environment.GetEnvironmentVariable("BALANCEBOARD_DEV"),
            "1",
            StringComparison.Ordinal);

        return new StartupOptions
        {
            SkipProcessCleanup = devMode || HasFlag(args, "--no-cleanup", "--dev"),
            SkipSingleInstance = devMode || HasFlag(args, "--allow-multiple", "--dev"),
            ConnectOnLaunch = HasFlag(args, "--connect"),
        };
    }

    private static bool HasFlag(string[] args, params string[] flags) =>
        args.Any(a => flags.Contains(a, StringComparer.OrdinalIgnoreCase));
}
