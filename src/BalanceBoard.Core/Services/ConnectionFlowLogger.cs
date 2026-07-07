namespace BalanceBoard.Core.Services;

/// <summary>
/// Standard connection-flow log lines — use category <c>CONNECT</c> for grep-friendly session logs.
/// </summary>
public static class ConnectionFlowLogger
{
    public static void LogIntent(Action<string>? log, ConnectionIntent intent) =>
        log?.Invoke($"[CONNECT] Intent={intent}");

    public static void LogHidDiscovery(Action<string>? log, IReadOnlyList<string> deviceIds)
    {
        if (deviceIds.Count == 0)
        {
            log?.Invoke("[CONNECT] HID discovery: no Wii devices visible.");
            return;
        }

        log?.Invoke($"[CONNECT] HID discovery: {deviceIds.Count} device(s): {string.Join(", ", deviceIds)}");
    }

    public static void LogHidAttempt(Action<string>? log, int index, string? deviceId) =>
        log?.Invoke($"[CONNECT] HID attempt index={index} id={deviceId ?? "unknown"}");

    public static void LogHidSuccess(Action<string>? log, string? deviceId, bool isBalanceBoard) =>
        log?.Invoke(
            isBalanceBoard
                ? $"[CONNECT] HID success id={deviceId ?? "unknown"} (balance board)"
                : $"[CONNECT] HID connected id={deviceId ?? "unknown"} but device is NOT a balance board");

    public static void LogHidFailure(Action<string>? log, int index, string reason) =>
        log?.Invoke($"[CONNECT] HID failed index={index}: {reason}");

    public static void LogPairingRound(Action<string>? log, int round, int totalRounds, bool removeStale) =>
        log?.Invoke($"[CONNECT] Pairing round {round}/{totalRounds} (removeStale={removeStale})");

    public static void LogFlowComplete(Action<string>? log, bool success) =>
        log?.Invoke(success ? "[CONNECT] Flow complete: connected." : "[CONNECT] Flow complete: not connected.");
}
