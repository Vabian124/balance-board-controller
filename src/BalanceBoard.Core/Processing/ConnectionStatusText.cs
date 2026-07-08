using BalanceBoard.Core.Models;
using BalanceBoard.Core.Services;

namespace BalanceBoard.Core.Processing;

/// <summary>
/// User-facing connection status strings for the dashboard.
/// Shared by WPF MainWindow and unit tests.
/// </summary>
public static class ConnectionStatusText
{
    public static string FormatStatusForUser(string status, UiDetailLevel detailLevel)
    {
        if (detailLevel != UiDetailLevel.Simple)
        {
            return status;
        }

        if (status.Contains("Connected", StringComparison.OrdinalIgnoreCase))
        {
            return "Connected!";
        }

        if (status.Contains("SYNC", StringComparison.OrdinalIgnoreCase)
            || status.Contains("pair", StringComparison.OrdinalIgnoreCase))
        {
            return "Need help pairing — ask a grown-up to press SYNC under the battery cover.";
        }

        if (status.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
        {
            return "Waiting for Bluetooth…";
        }

        if (status.Contains("again", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Finding", StringComparison.OrdinalIgnoreCase))
        {
            return status;
        }

        return "Working on it…";
    }

    public static string FormatConnectFailure(
        ConnectionIntent intent,
        ConnectResult result,
        UiDetailLevel detailLevel) =>
        result.Status switch
        {
            ConnectStatus.Cancelled => "Cancelled.",
            ConnectStatus.NoDevices => detailLevel == UiDetailLevel.Simple
                ? "Board not found — we'll keep trying if auto-connect is on."
                : intent == ConnectionIntent.QuickReconnect
                    ? "Board offline — turn it on or press SYNC, then click Connect."
                    : "Not found — press SYNC, then Connect again.",
            ConnectStatus.PairingFailed => detailLevel == UiDetailLevel.Simple
                ? "Could not find the board — ask a grown-up to press SYNC, then Connect."
                : "Press SYNC on the board, then click Connect.",
            _ => detailLevel == UiDetailLevel.Simple
                ? "Something went wrong — see the log for details."
                : result.Message ?? "Connection failed — see session log.",
        };
}
