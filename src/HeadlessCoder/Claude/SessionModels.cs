namespace HeadlessCoder.Claude;

/// <summary>A Claude Code project = a working directory that has one or more sessions.</summary>
public sealed record ProjectInfo(
    string Id,           // encoded directory name under ~/.claude/projects
    string Path,         // real working directory (from session content)
    int SessionCount,
    DateTimeOffset? LastActivity);

/// <summary>Summary of a single Claude Code session (one .jsonl transcript).</summary>
public sealed record SessionSummary(
    string Id,
    string ProjectId,
    string Cwd,
    string Title,
    string? GitBranch,
    int MessageCount,
    DateTimeOffset? LastActivity);

/// <summary>A single rendered message inside a transcript.</summary>
public sealed record TranscriptMessage(
    string Role,          // "user" | "assistant" | "tool"
    string Text,
    DateTimeOffset? Timestamp,
    string? ToolName = null);

/// <summary>Request body for sending a message to a (possibly new) session.</summary>
public sealed class SendMessageRequest
{
    public string? SessionId { get; set; }

    /// <summary>
    /// When true, start a fresh session using <see cref="SessionId"/> as its id
    /// (claude --session-id). When false, resume the existing <see cref="SessionId"/>.
    /// Set server-side; not trusted from the client body.
    /// </summary>
    public bool IsNewSession { get; set; }

    public string Cwd { get; set; } = "";
    public string Message { get; set; } = "";
    public string PermissionMode { get; set; } = "default"; // default|acceptEdits|plan|bypassPermissions
    public string? Model { get; set; }
}
