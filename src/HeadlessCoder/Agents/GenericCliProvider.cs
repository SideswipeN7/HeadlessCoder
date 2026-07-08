using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Base provider for agent CLIs that we drive in a simple "one prompt in, text
/// out" fashion (Gemini CLI, Copilot CLI, ...). Reading past transcripts and
/// resuming sessions are not attempted here — each message is a fresh headless
/// invocation whose stdout is streamed to the UI as text.
/// </summary>
public abstract class GenericCliProvider : IAgentProvider
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }

    /// <summary>Base executable name to locate (e.g. "gemini").</summary>
    protected abstract string ExecutableName { get; }

    /// <summary>Config directory whose presence hints the CLI has been set up.</summary>
    protected abstract string? ConfigDirectory { get; }

    /// <summary>Human-readable install/auth hint shown by the doctor.</summary>
    protected abstract string InstallHint { get; }

    /// <summary>Builds the CLI arguments for a one-shot headless prompt.</summary>
    protected abstract IEnumerable<string> BuildArgs(SendMessageRequest request);

    protected virtual string VersionArg => "--version";

    public AgentDescriptor Detect()
    {
        string? exe = Cli.Locate(ExecutableName);
        string? cfg = ConfigDirectory;
        bool cfgFound = cfg is not null && Directory.Exists(cfg);

        var d = new AgentDescriptor
        {
            Id = Id,
            DisplayName = DisplayName,
            SupportsHistory = false,
            SupportsResume = false,
            Installed = exe is not null,
            ExecutablePath = exe,
            ConfigFound = cfgFound,
            SessionStorePath = cfg,
            SessionCount = 0,
        };
        if (exe is not null)
            d.Version = Cli.ProbeVersion(exe, VersionArg);
        if (!d.Installed)
            d.Remediation = InstallHint;
        return d;
    }

    // History is not supported for generic providers.
    public IReadOnlyList<SessionSummary> ListSessions() => Array.Empty<SessionSummary>();
    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId) =>
        Array.Empty<TranscriptMessage>();

    public async IAsyncEnumerable<AgentEvent> SendAsync(
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? exe = Cli.Locate(ExecutableName);
        if (exe is null)
        {
            yield return AgentEvent.Error($"{DisplayName} ('{ExecutableName}') was not found on this machine. {InstallHint}");
            yield break;
        }

        yield return AgentEvent.System(request.SessionId);

        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Directory.Exists(request.Cwd) ? request.Cwd : Environment.CurrentDirectory,
        };
        foreach (var a in BuildArgs(request))
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        Exception? startError = null;
        try
        {
            process.Start();
            process.BeginErrorReadLine();
            process.StandardInput.Close();
        }
        catch (Exception ex)
        {
            startError = ex;
        }
        if (startError is not null)
        {
            yield return AgentEvent.Error($"Failed to start {DisplayName}: {startError.Message}");
            yield break;
        }

        await using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

        // Stream stdout line-by-line so the user sees progress.
        var reader = process.StandardOutput;
        string? line;
        bool sawOutput = false;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            sawOutput = true;
            yield return AgentEvent.TextDelta(line + "\n");
        }

        try { await process.WaitForExitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { yield break; }

        if (process.ExitCode != 0)
        {
            string err = stderr.ToString().Trim();
            yield return AgentEvent.Error(err.Length > 0
                ? err
                : $"{DisplayName} exited with code {process.ExitCode}.");
        }
        else if (!sawOutput && stderr.Length > 0)
        {
            yield return AgentEvent.Assistant(stderr.ToString().Trim());
        }

        yield return new AgentEvent { Kind = "result", IsError = process.ExitCode != 0 };
    }
}
