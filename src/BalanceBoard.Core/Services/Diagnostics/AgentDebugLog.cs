using System.Text.Json;

namespace BalanceBoard.Core.Services.Diagnostics;

/// <summary>Session-scoped NDJSON debug log for agent investigation (remove after verification).</summary>
public static class AgentDebugLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "wiiboardguirevamp",
        "debug-577eae.log");

    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        try
        {
            var payload = new
            {
                sessionId = "577eae",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId,
            };
            File.AppendAllText(LogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }
}
