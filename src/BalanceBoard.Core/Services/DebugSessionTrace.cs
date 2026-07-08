namespace BalanceBoard.Core.Services;

/// <summary>
/// No-op stub for a past debug session's NDJSON trace hooks left scattered through the connect
/// flow (<see cref="BalanceBoardConnection"/>, <see cref="BluetoothPairingService"/>,
/// <see cref="BalanceBoardSession"/>, etc.). The previous implementation unconditionally wrote
/// every call to a hardcoded developer-machine absolute path (outside any app data directory)
/// plus a second copy under <c>%AppData%\BalanceBoardApp\</c> on every connect/wake/HID-probe —
/// an unbounded, never-rotated file growing forever in production, and a personal path baked
/// into a shipped library (see AGENTS.md "Do not" / INSTRUCTIONS.md personal-path rules).
/// Kept as a no-op instead of deleting call sites to keep this a minimal, safe diff.
/// </summary>
public static class DebugSessionTrace
{
    public static void Write(string location, string message, string hypothesisId, object? data = null, string runId = "pre-fix")
    {
    }
}
