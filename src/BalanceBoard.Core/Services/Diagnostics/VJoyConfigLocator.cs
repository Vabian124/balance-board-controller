using System.IO;

namespace BalanceBoard.Core.Services.Diagnostics;

/// <summary>
/// Locates the official vJoy configuration utility (<c>vJoyConf.exe</c>).
/// </summary>
public static class VJoyConfigLocator
{
    public const string ConfigExeName = "vJoyConf.exe";

    public static IReadOnlyList<string> CandidatePaths()
    {
        var candidates = new List<string>();
        foreach (var root in ProgramFilesRoots())
        {
            candidates.Add(Path.Combine(root, "vJoy", "x64", ConfigExeName));
            candidates.Add(Path.Combine(root, "vJoy", "x86", ConfigExeName));
            candidates.Add(Path.Combine(root, "vJoy", ConfigExeName));
        }

        return candidates;
    }

    public static string? FindConfigExe() => FindConfigExe(File.Exists);

    public static string? FindConfigExe(Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        foreach (var candidate in CandidatePaths())
        {
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> ProgramFilesRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in new[] { "ProgramW6432", "ProgramFiles", "ProgramFiles(x86)" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
            {
                yield return value;
            }
        }

        foreach (var fallback in new[] { @"C:\Program Files", @"C:\Program Files (x86)" })
        {
            if (seen.Add(fallback))
            {
                yield return fallback;
            }
        }
    }
}
