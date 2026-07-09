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

    /// <summary>Base executable name to locate (e.g. "agy").</summary>
    protected abstract string ExecutableName { get; }

    /// <summary>Config directory whose presence hints the CLI has been set up.</summary>
    protected abstract string? ConfigDirectory { get; }

    /// <summary>Human-readable install/auth hint shown by the doctor.</summary>
    protected abstract string InstallHint { get; }

    /// <summary>Builds the CLI arguments for a one-shot headless prompt.</summary>
    protected abstract IEnumerable<string> BuildArgs(SendMessageRequest request);

    protected virtual string VersionArg => "--version";

    /// <summary>
    /// Optional reader for this CLI's past sessions. Subclasses whose on-disk
    /// transcript format we understand return a store; the default is none.
    /// </summary>
    protected virtual ICliHistoryStore? HistoryStore => null;

    /// <summary>Working modes offered in the composer. Values map to <see cref="BuildArgs"/>.</summary>
    protected static readonly AgentOption[] DefaultModes =
    {
        new("default", "Default"),
        new("bypassPermissions", "Auto-approve all"),
    };

    /// <summary>Extra --model choices this CLI offers (the UI prepends a "Default").</summary>
    protected virtual IReadOnlyList<AgentOption> ModelOptions => Array.Empty<AgentOption>();
    protected virtual IReadOnlyList<AgentOption> PermissionModeOptions => DefaultModes;
    protected virtual bool SupportsEffortFlag => false;

    public AgentDescriptor Detect()
    {
        string? exe = Cli.Locate(ExecutableName);
        string? cfg = ConfigDirectory;
        bool cfgFound = cfg is not null && Directory.Exists(cfg);

        var store = HistoryStore;
        bool hasHistory = store?.IsAvailable == true;
        int sessionCount = hasHistory ? store!.ListSessions(Id).Count : 0;

        var d = new AgentDescriptor
        {
            Id = Id,
            DisplayName = DisplayName,
            SupportsHistory = hasHistory,
            SupportsResume = false,
            Installed = exe is not null,
            ExecutablePath = exe,
            ConfigFound = cfgFound,
            SessionStorePath = store?.StorePath ?? cfg,
            SessionCount = sessionCount,
            Models = ModelOptions,
            PermissionModes = PermissionModeOptions,
            SupportsEffort = SupportsEffortFlag,
        };
        if (exe is not null)
            d.Version = Cli.ProbeVersion(exe, VersionArg);
        if (!d.Installed)
            d.Remediation = InstallHint;
        return d;
    }

    public IReadOnlyList<SessionSummary> ListSessions() =>
        HistoryStore?.ListSessions(Id) ?? Array.Empty<SessionSummary>();
    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId) =>
        HistoryStore?.GetTranscript(projectId, sessionId) ?? Array.Empty<TranscriptMessage>();

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
