namespace BalanceBoard.App.Services;

public sealed class StartupOptions
{
    public bool SkipProcessCleanup { get; init; }
    public bool SkipSingleInstance { get; init; }
    public bool ConnectOnLaunch { get; init; }
    public bool SimulateBoard { get; init; }
    public string? PhysicalTestScenario { get; init; }
    public int AutoExitAfterSeconds { get; init; }

    public bool HardwareTestMode => !string.IsNullOrWhiteSpace(PhysicalTestScenario);

    public static StartupOptions Parse(string[] args)
    {
        var devMode = string.Equals(
            Environment.GetEnvironmentVariable("BALANCEBOARD_DEV"),
            "1",
            StringComparison.Ordinal);

        var simulate = HasFlag(args, "--simulate-board")
            || string.Equals(Environment.GetEnvironmentVariable("BALANCEBOARD_SIMULATE"), "1", StringComparison.Ordinal);

        return new StartupOptions
        {
            SkipProcessCleanup = devMode || HasFlag(args, "--no-cleanup", "--dev"),
            SkipSingleInstance = devMode || HasFlag(args, "--allow-multiple", "--dev"),
            ConnectOnLaunch = HasFlag(args, "--connect") || simulate,
            SimulateBoard = simulate,
            PhysicalTestScenario = ParseStringFlag(args, "--physical-test"),
            AutoExitAfterSeconds = ParseIntFlag(args, "--auto-exit-after", 0),
        };
    }

    private static string? ParseStringFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                var value = args[i + 1];
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }

    private static int ParseIntFlag(string[] args, string flag, int defaultValue)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var value))
            {
                return value;
            }
        }

        return defaultValue;
    }

    private static bool HasFlag(string[] args, params string[] flags) =>
        args.Any(a => flags.Contains(a, StringComparer.OrdinalIgnoreCase));
}
