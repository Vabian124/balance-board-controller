namespace BalanceBoard.Core.Services;

/// <summary>Tracks the active connect attempt for correlation in logs and fatal exception context.</summary>
public static class ConnectFlowContext
{
    private static string? _currentCorrelationId;

    public static string? CurrentCorrelationId => _currentCorrelationId;

    public static string BeginAttempt()
    {
        _currentCorrelationId = $"conn-{DateTime.UtcNow:HHmmss}-{Random.Shared.Next(1000, 9999)}";
        return _currentCorrelationId;
    }

    public static void EndAttempt() => _currentCorrelationId = null;

    public static string Prefix(string? correlationId = null)
    {
        var id = correlationId ?? _currentCorrelationId;
        return string.IsNullOrWhiteSpace(id) ? string.Empty : $"corr={id} ";
    }
}
