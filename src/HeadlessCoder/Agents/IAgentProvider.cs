using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// An agent CLI backend (Claude Code, Gemini CLI, Copilot CLI, ...). Providers
/// know how to detect their CLI, read past sessions (if supported), and stream a
/// message as normalized <see cref="AgentEvent"/>s.
/// </summary>
public interface IAgentProvider
{
    string Id { get; }
    string DisplayName { get; }

    /// <summary>Inspect the machine and report install/auth/capability status.</summary>
    AgentDescriptor Detect();

    /// <summary>Past sessions for this agent (empty when history isn't supported).</summary>
    IReadOnlyList<SessionSummary> ListSessions();

    /// <summary>Rendered transcript for a session (empty when unsupported).</summary>
    IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId);

    /// <summary>Send a message and stream normalized events back.</summary>
    IAsyncEnumerable<AgentEvent> SendAsync(SendMessageRequest request, CancellationToken ct);

    /// <summary>Delete a session's stored transcript (for in-private sessions). No-op if unsupported.</summary>
    bool PurgeSession(string sessionId) => false;
}
