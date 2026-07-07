namespace BalanceBoard.Core.Services;

internal static class SafeCallbacks
{
    public static void Raise(Action<string>? handler, string message)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(message);
        }
        catch
        {
            // Subscriber faults must not take down the worker or UI.
        }
    }

    public static void Raise(Action<Models.ProcessedBalance>? handler, Models.ProcessedBalance data)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(data);
        }
        catch
        {
            // Subscriber faults must not take down the worker or UI.
        }
    }

    public static void Raise(Action<Models.ConnectionPhase>? handler, Models.ConnectionPhase phase)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            handler(phase);
        }
        catch
        {
            // Subscriber faults must not take down the worker or UI.
        }
    }

    public static void Raise(Action? handler)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            handler();
        }
        catch
        {
            // Subscriber faults must not take down the worker or UI.
        }
    }
}
