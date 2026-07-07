namespace BalanceBoard.Core.Services;

public sealed class FileLogService
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly string _currentLogPath;

    public FileLogService(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BalanceBoardApp",
            "logs");
        Directory.CreateDirectory(root);
        _logDirectory = root;
        _currentLogPath = Path.Combine(root, $"session-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public string LogDirectory => _logDirectory;

    public string CurrentLogPath => _currentLogPath;

    public event Action<string>? LineWritten;

    public void Write(string message, string category = "INFO")
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
        lock (_sync)
        {
            File.AppendAllText(_currentLogPath, line + Environment.NewLine);
        }

        LineWritten?.Invoke(line);
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
