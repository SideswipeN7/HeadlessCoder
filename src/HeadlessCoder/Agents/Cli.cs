using System.Diagnostics;
using System.Text;

namespace HeadlessCoder.Agents;

/// <summary>Shared helpers for locating agent CLI executables and probing versions.</summary>
public static class Cli
{
    /// <summary>
    /// Finds an executable by base name, searching PATH and a set of common
    /// install locations. Returns the full path, or null if not found.
    /// </summary>
    public static string? Locate(string baseName, params string[] extraDirs)
    {
        string[] names = OperatingSystem.IsWindows()
            ? new[] { baseName + ".exe", baseName + ".cmd", baseName + ".bat", baseName }
            : new[] { baseName };

        // 1) PATH
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        var dirs = new List<string>();
        if (pathVar is not null)
            dirs.AddRange(pathVar.Split(Path.PathSeparator));

        // 2) Common install locations
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        dirs.Add(Path.Combine(home, ".local", "bin"));
        dirs.Add(Path.Combine(home, "bin"));
        dirs.Add("/usr/local/bin");
        dirs.Add("/opt/homebrew/bin");
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
            dirs.Add(Path.Combine(appData, "npm")); // global npm bins on Windows
        dirs.AddRange(extraDirs);

        foreach (var dir in dirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            foreach (var name in names)
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), name);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { /* ignore malformed PATH entries */ }
            }
        }
        return null;
    }

    /// <summary>Runs "&lt;exe&gt; --version" (best effort) and returns the first output line.</summary>
    public static string? ProbeVersion(string exe, string arg = "--version", int timeoutMs = 4000)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, arg)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;

            string outp = p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(true); } catch { }
                return null;
            }

            string text = (outp + "\n" + err).Trim();
            string? first = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
        }
        catch
        {
            return null;
        }
    }
}
