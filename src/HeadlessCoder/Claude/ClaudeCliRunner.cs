using System.Diagnostics;
using System.Text;

namespace HeadlessCoder.Claude;

/// <summary>
/// Drives the `claude` CLI in headless streaming mode and yields raw stream-json
/// lines as they arrive. Each yielded string is one JSON object (a stream event).
/// </summary>
public sealed class ClaudeCliRunner
{
    private readonly string _executable;

    public ClaudeCliRunner(string? executable = null)
    {
        _executable = executable ?? LocateClaude();
    }

    public string Executable => _executable;

    /// <summary>Streams stream-json events for a message sent to a (possibly new) session.</summary>
    public async IAsyncEnumerable<string> SendAsync(
        SendMessageRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var args = BuildArgs(request);

        var psi = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = ResolveWorkingDirectory(request.Cwd),
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start the claude process.");

        process.BeginErrorReadLine();
        process.StandardInput.Close();

        // Ensure the process dies if the client disconnects / request is cancelled.
        await using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* ignored */ }
        });

        var reader = process.StandardOutput;
        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (line.Length > 0)
                yield return line;
        }

        try { await process.WaitForExitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { yield break; }

        if (process.ExitCode != 0 && stderr.Length > 0)
        {
            // Surface CLI failures to the client as a synthetic error event.
            string err = stderr.ToString().Replace("\"", "'").Trim();
            yield return $"{{\"type\":\"error\",\"error\":\"{JsonEscape(err)}\"}}";
        }
    }

    /// <summary>
    /// Builds the <c>claude</c> CLI argument list for a message. Extracted so the exact
    /// invocation (headless streaming flags, new-vs-resume, model/effort) can be unit-tested.
    /// </summary>
    public static IReadOnlyList<string> BuildArgs(SendMessageRequest request)
    {
        var args = new List<string>
        {
            "--print", request.Message,
            "--output-format", "stream-json",
            "--include-partial-messages",
            "--verbose",
            "--permission-mode", NormalizePermissionMode(request.PermissionMode),
        };

        // The server assigns a fresh id for new sessions and flags IsNewSession so we
        // know to create (--session-id) rather than resume (--resume) it.
        bool isNew = request.IsNewSession || string.IsNullOrWhiteSpace(request.SessionId);
        string sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId!;

        args.Add(isNew ? "--session-id" : "--resume");
        args.Add(sessionId);

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            args.Add("--model");
            args.Add(request.Model!);
        }

        if (IsValidEffort(request.Effort))
        {
            args.Add("--effort");
            args.Add(request.Effort!);
        }

        return args;
    }

    internal static bool IsValidEffort(string? effort) => effort is
        "low" or "medium" or "high" or "xhigh" or "max";

    internal static string NormalizePermissionMode(string mode) => mode switch
    {
        "acceptEdits" or "plan" or "bypassPermissions" or "default" => mode,
        _ => "default",
    };

    private static string ResolveWorkingDirectory(string cwd)
    {
        if (!string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd))
            return cwd;
        return Environment.CurrentDirectory;
    }

    private static string LocateClaude()
    {
        string exeName = OperatingSystem.IsWindows() ? "claude.exe" : "claude";

        // 1) PATH
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is not null)
        {
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                string candidate = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(candidate)) return candidate;
            }
        }

        // 2) Common install location (~/.local/bin)
        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "bin", exeName);
        if (File.Exists(local)) return local;

        // 3) Fall back to bare name and hope it resolves at launch time.
        return exeName;
    }

    internal static string JsonEscape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
}
