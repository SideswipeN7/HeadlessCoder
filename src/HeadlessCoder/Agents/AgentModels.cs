namespace HeadlessCoder.Agents;

/// <summary>
/// A normalized event streamed from any agent CLI to the browser. Provider
/// implementations translate their native output (Claude stream-json, plain
/// stdout, etc.) into this shape so the UI is agent-agnostic.
/// </summary>
public sealed class AgentEvent
{
    /// <summary>
    /// One of: system | text_delta | assistant | tool | tool_result | result | error
    /// </summary>
    public string Kind { get; init; } = "";

    public string? Text { get; init; }
    public string? ToolName { get; init; }
    public string? SessionId { get; init; }

    // result-only fields
    public double? DurationMs { get; init; }
    public double? CostUsd { get; init; }
    public int? Turns { get; init; }
    public bool? IsError { get; init; }

    // error-only field
    public string? Message { get; init; }

    public static AgentEvent System(string? sessionId) => new() { Kind = "system", SessionId = sessionId };
    public static AgentEvent TextDelta(string text) => new() { Kind = "text_delta", Text = text };
    public static AgentEvent Assistant(string text) => new() { Kind = "assistant", Text = text };
    public static AgentEvent Tool(string name, string? detail) => new() { Kind = "tool", ToolName = name, Text = detail };
    public static AgentEvent ToolResult(string name, string? detail) => new() { Kind = "tool_result", ToolName = name, Text = detail };
    public static AgentEvent Error(string message) => new() { Kind = "error", Message = message };
}

/// <summary>
/// Capabilities + install/auth status for one agent CLI, surfaced to the doctor
/// screen and the UI.
/// </summary>
public sealed class AgentDescriptor
{
    public required string Id { get; init; }          // "claude" | "antigravity" | "copilot"
    public required string DisplayName { get; init; }

    public bool Installed { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Version { get; set; }

    public bool ConfigFound { get; set; }
    public string? SessionStorePath { get; set; }
    public int SessionCount { get; set; }

    public bool SupportsHistory { get; init; }        // can we read past transcripts?
    public bool SupportsResume { get; init; }         // can we continue a session by id?

    /// <summary>ready | partial | missing</summary>
    public string Status =>
        !Installed ? "missing"
        : (SupportsHistory && !ConfigFound) ? "partial"
        : "ready";

    /// <summary>What the user should do if this agent isn't fully usable.</summary>
    public string? Remediation { get; set; }
}

/// <summary>Startup diagnostics: what was detected and what the user must do.</summary>
public sealed class DoctorReport
{
    public required IReadOnlyList<AgentDescriptor> Agents { get; init; }
    public bool AnyAgentAvailable => Agents.Any(a => a.Installed);
    public int TotalSessions => Agents.Sum(a => a.SessionCount);
}
