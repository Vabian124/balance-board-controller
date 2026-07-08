using System.Runtime.CompilerServices;

namespace BalanceBoard.Testing;

public static class TestEnvironment
{
    [ModuleInitializer]
    internal static void EnableDevModeForTestHost()
    {
        Environment.SetEnvironmentVariable("BALANCEBOARD_DEV", "1");
    }
}
