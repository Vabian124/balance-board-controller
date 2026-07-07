using BalanceBoard.Core.Models;

namespace BalanceBoard.Core.Services;

public sealed class FileLogService
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly string _currentLogPath;

    public FileLogService(string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppDataPaths.LogsDirectory;
        Directory.CreateDirectory(root);
        _logDirectory = root;
        _currentLogPath = Path.Combine(root, $"session-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public string LogDirectory => _logDirectory;

    public string CurrentLogPath => _currentLogPath;

    public event Action<string>? LineWritten;

    public void WriteSessionHeader(string settingsPath, AppSettings settings)
    {
        Write("=== Session start ===", "SESSION");
        Write($"Settings file: {settingsPath}", "SESSION");
        Write(
            $"Flags: HasConnectedBefore={settings.HasConnectedBefore}, " +
            $"AutoConnectOnStartup={settings.AutoConnectOnStartup}, " +
            $"Profile={settings.ActiveProfileName}",
            "SESSION");

        if (!string.IsNullOrWhiteSpace(settings.LastConnectedDeviceId))
        {
            var when = settings.LastConnectedAtUtc?.ToString("u") ?? "unknown";
            Write($"Last board: {settings.LastConnectedDeviceId} at {when}", "SESSION");
        }
        else
        {
            Write("Last board: (none saved yet)", "SESSION");
        }

        Write($"Log file: {_currentLogPath}", "SESSION");
    }

    public void Write(string message, string category = "INFO")
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
        lock (_sync)
        {
            File.AppendAllText(_currentLogPath, line + Environment.NewLine);
        }

        LineWritten?.Invoke(line);
    }

    public void WriteException(Exception exception, string context)
    {
        Write($"{context}: {exception.GetType().Name}: {exception.Message}", "ERROR");
        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            foreach (var frame in exception.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                Write(frame.Trim(), "ERROR");
            }
        }

        if (exception.InnerException is not null)
        {
            WriteException(exception.InnerException, $"{context} (inner)");
        }
    }

    public string ReadTail(int maxLines = 200)
    {
        if (!File.Exists(_currentLogPath))
        {
            return string.Empty;
        }

        lock (_sync)
        {
            var lines = File.ReadAllLines(_currentLogPath);
            if (lines.Length <= maxLines)
            {
                return string.Join(Environment.NewLine, lines);
            }

            return string.Join(Environment.NewLine, lines[^maxLines..]);
        }
    }

    public void ClearCurrentSessionLog()
    {
        lock (_sync)
        {
            if (File.Exists(_currentLogPath))
            {
                File.WriteAllText(_currentLogPath, string.Empty);
            }
        }
    }
}
