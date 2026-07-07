using System.Runtime.InteropServices;
using System.Text;

namespace BalanceBoard.Core.Services;

/// <summary>Formats exceptions for session log files — full detail for diagnosis.</summary>
public static class ExceptionLogFormatter
{
    public static string FormatHeader(Exception exception, string context, IReadOnlyList<string>? tags = null)
    {
        var tagPart = tags is { Count: > 0 }
            ? $" tags=[{string.Join(",", tags)}]"
            : string.Empty;

        var hresult = exception.HResult != 0
            ? $" HRESULT=0x{exception.HResult:X8}"
            : string.Empty;

        var thread = Thread.CurrentThread;
        var threadLabel = string.IsNullOrWhiteSpace(thread.Name)
            ? $"tid={thread.ManagedThreadId}"
            : $"thread={thread.Name} tid={thread.ManagedThreadId}";

        return
            $"EXCEPTION context={context}{tagPart} type={exception.GetType().FullName} " +
            $"message={exception.Message}{hresult} {threadLabel}";
    }

    public static IEnumerable<string> FormatChain(Exception exception)
    {
        var depth = 0;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var prefix = depth == 0 ? "  " : $"  inner[{depth}]: ";
            yield return $"{prefix}{current.GetType().FullName}: {current.Message}";

            if (current.HResult != 0 && depth > 0)
            {
                yield return $"    HRESULT=0x{current.HResult:X8}";
            }

            depth++;
        }
    }

    public static IEnumerable<string> FormatStackTrace(Exception exception)
    {
        if (string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            yield return "  (no stack trace)";
            yield break;
        }

        yield return "  stack:";
        foreach (var frame in exception.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return $"    {frame.Trim()}";
        }
    }

    public static string ShortUiMessage(Exception exception) =>
        exception.InnerException is null
            ? exception.Message
            : $"{exception.Message} ({exception.InnerException.GetType().Name}: {exception.InnerException.Message})";

    public static bool IsBenignDeviceIo(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is IOException or ObjectDisposedException)
            {
                return true;
            }

            if (current.Message.Contains("device is not connected", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("ThreadPoolBoundHandle", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("The device is not ready", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
