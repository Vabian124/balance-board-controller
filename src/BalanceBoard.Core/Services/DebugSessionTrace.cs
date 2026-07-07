using System.Text.Json;

namespace BalanceBoard.Core.Services;

/// <summary>NDJSON debug trace for active debug session (removed after verification).</summary>
public static class DebugSessionTrace
{
    private const string LogPath = @"c:\Users\fcsta\Downloads\wiiboardguirevamp\debug-c06057.log";

    public static void Write(string location, string message, string hypothesisId, object? data = null, string runId = "pre-fix")
    {
        // #region agent log
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = "c06057",
                ["runId"] = runId,
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
            File.AppendAllText(LogPath, line);
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BalanceBoardApp",
                "debug-c06057.log");
            File.AppendAllText(fallback, line);
        }
        catch
        {
            // Best-effort only.
        }
        // #endregion
    }
}
