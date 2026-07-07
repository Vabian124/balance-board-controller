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
                WriteFatal(ex, args.IsTerminating ? "AppDomain terminating" : "AppDomain");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteFatal(args.Exception, "UnobservedTask");
            args.SetObserved();
        };
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
