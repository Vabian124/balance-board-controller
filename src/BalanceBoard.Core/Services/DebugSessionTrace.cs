namespace BalanceBoard.Core.Services;

/// <summary>
/// No-op stub for past debug-session NDJSON trace hooks left in the connect flow.
/// </summary>
public static class DebugSessionTrace
{
    public static void Write(string location, string message, string hypothesisId, object? data = null, string runId = "pre-fix")
    {
    }
}
