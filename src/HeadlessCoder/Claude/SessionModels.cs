namespace HeadlessCoder.Claude;

/// <summary>A Claude Code project = a working directory that has one or more sessions.</summary>
public sealed record ProjectInfo(
    string Id,           // encoded directory name under ~/.claude/projects
    string Path,         // real working directory (from session content)
    int SessionCount,
    DateTimeOffset? LastActivity);

/// <summary>Summary of a single agent session (one transcript).</summary>
public sealed record SessionSummary(
    string Id,
    string ProjectId,
    string Cwd,
    string Title,
    string? GitBranch,
    int MessageCount,
    DateTimeOffset? LastActivity,
    string Provider = "claude");

/// <summary>Last-known token usage for a session, shown on entry (before any new turn).</summary>
public sealed record AgentUsage(
    long? InputTokens,
    long? OutputTokens,
    long? CacheReadTokens,
    long? CacheCreateTokens,
    long? ContextTokens,
    long? ContextWindow,
    string? Model);

/// <summary>A single rendered message inside a transcript.</summary>
public sealed record TranscriptMessage(
    string Role,          // "user" | "assistant" | "tool"
    string Text,
    DateTimeOffset? Timestamp,
    string? ToolName = null);

/// <summary>Request body for sending a message to a (possibly new) session.</summary>
public sealed class SendMessageRequest
{
    /// <summary>Which agent CLI to use: "claude" | "antigravity" | "copilot".</summary>
    public string Provider { get; set; } = "claude";

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

    /// <summary>Reasoning effort level (Claude: low|medium|high|xhigh|max).</summary>
    public string? Effort { get; set; }
}
