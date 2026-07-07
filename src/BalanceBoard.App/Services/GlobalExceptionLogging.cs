using System.IO;
using BalanceBoard.Core.Services;

namespace BalanceBoard.App.Services;

/// <summary>
/// Routes process-wide unhandled exceptions to the session log file.
/// </summary>
public static class GlobalExceptionLogging
{
    private static FileLogService? _log;
    private static bool _registered;

    public static void Register(FileLogService log)
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        _log = log;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                if (IsBenignDeviceIo(ex))
                {
                    _log?.Write($"[DISCONNECT] Ignored benign device I/O: {ex.Message}", "WARN");
                    return;
                }

                WriteFatal(ex, args.IsTerminating ? "AppDomain terminating" : "AppDomain");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            foreach (var ex in args.Exception.Flatten().InnerExceptions)
            {
                if (IsBenignDeviceIo(ex))
                {
                    _log?.Write($"[DISCONNECT] Ignored benign device I/O: {ex.Message}", "WARN");
                    continue;
                }

                WriteFatal(ex, "UnobservedTask");
            }

            args.SetObserved();
        };
    }

    private static bool IsBenignDeviceIo(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is IOException or ObjectDisposedException)
            {
                return true;
            }

            if (current.Message.Contains("device is not connected", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("ThreadPoolBoundHandle", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void WriteFatal(Exception exception, string context)
    {
        try
        {
            _log?.WriteException(exception, $"FATAL {context}");
        }
        catch
        {
            // Last resort — never throw from logging.
        }
    }
}
