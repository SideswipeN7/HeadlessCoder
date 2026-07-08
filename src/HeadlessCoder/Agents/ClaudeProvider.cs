using System.Runtime.CompilerServices;
using System.Text.Json;
using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Fully-featured provider for Anthropic's Claude Code CLI: reads on-disk
/// transcripts, resumes sessions, and streams stream-json output.
/// </summary>
public sealed class ClaudeProvider : IAgentProvider
{
    private readonly ClaudeSessionStore _store;
    private readonly ClaudeCliRunner _runner;

    public ClaudeProvider(ClaudeSessionStore store, ClaudeCliRunner runner)
    {
        _store = store;
        _runner = runner;
    }

    public string Id => "claude";
    public string DisplayName => "Claude Code";

    public AgentDescriptor Detect()
    {
        string? exe = Cli.Locate("claude");
        var d = new AgentDescriptor
        {
            Id = Id,
            DisplayName = DisplayName,
            SupportsHistory = true,
            SupportsResume = true,
            Installed = exe is not null,
            ExecutablePath = exe,
            ConfigFound = _store.IsAvailable,
            SessionStorePath = _store.ProjectsRoot,
        };

        if (exe is not null)
            d.Version = Cli.ProbeVersion(exe);
        d.SessionCount = _store.IsAvailable ? _store.ListSessions().Count : 0;

        if (!d.Installed)
            d.Remediation = "Install Claude Code (https://claude.com/claude-code) and run `claude` once to sign in.";
        else if (!d.ConfigFound)
            d.Remediation = "Run `claude` once and start a session so transcripts appear under ~/.claude/projects.";

        return d;
    }

    public IReadOnlyList<SessionSummary> ListSessions() =>
        _store.ListSessions().Select(s => s with { Provider = Id }).ToList();

    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId) =>
        _store.GetTranscript(projectId, sessionId);

    public async IAsyncEnumerable<AgentEvent> SendAsync(
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var line in _runner.SendAsync(request, ct).WithCancellation(ct))
        {
            foreach (var ev in Translate(line))
                yield return ev;
        }
    }

    /// <summary>Maps one Claude stream-json line to zero or more normalized events.</summary>
    private static IEnumerable<AgentEvent> Translate(string line)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(line);
            root = doc.RootElement.Clone();
        }
        catch { yield break; }

        string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        switch (type)
        {
            case "system":
                if (root.TryGetProperty("subtype", out var st) && st.GetString() == "init")
                    yield return AgentEvent.System(GetString(root, "session_id"));
                break;

            case "stream_event":
                if (root.TryGetProperty("event", out var ev) &&
                    ev.TryGetProperty("type", out var et) && et.GetString() == "content_block_delta" &&
                    ev.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("type", out var dt) && dt.GetString() == "text_delta")
                {
                    yield return AgentEvent.TextDelta(GetString(delta, "text") ?? "");
                }
                break;

            case "assistant":
                if (root.TryGetProperty("message", out var am) &&
                    am.TryGetProperty("content", out var ac) && ac.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in ac.EnumerateArray())
                    {
                        string bt = b.TryGetProperty("type", out var btt) ? btt.GetString() ?? "" : "";
                        if (bt == "text")
                        {
                            string s = GetString(b, "text") ?? "";
                            if (s.Trim().Length > 0) yield return AgentEvent.Assistant(s);
                        }
                        else if (bt == "tool_use")
                        {
                            string name = GetString(b, "name") ?? "tool";
                            string detail = b.TryGetProperty("input", out var inp) ? Compact(inp) : "";
                            yield return AgentEvent.Tool(name, detail);
                        }
                    }
                }
                break;

            case "user":
                if (root.TryGetProperty("message", out var um) &&
                    um.TryGetProperty("content", out var uc) && uc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in uc.EnumerateArray())
                    {
                        if (b.TryGetProperty("type", out var btt) && btt.GetString() == "tool_result")
                        {
                            string detail = b.TryGetProperty("content", out var rc) ? Flatten(rc) : "";
                            if (detail.Trim().Length > 0)
                                yield return AgentEvent.ToolResult("result", Truncate(detail, 4000));
                        }
                    }
                }
                break;

            case "result":
                yield return new AgentEvent
                {
                    Kind = "result",
                    DurationMs = GetDouble(root, "duration_ms"),
                    CostUsd = GetDouble(root, "total_cost_usd"),
                    Turns = (int?)GetDouble(root, "num_turns"),
                    IsError = root.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True,
                };
                break;

            case "error":
                yield return AgentEvent.Error(GetString(root, "error") ?? "unknown error");
                break;
        }
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? GetDouble(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string Compact(JsonElement el)
    {
        string s = el.GetRawText();
        return s.Length > 600 ? s[..600] + "…" : s;
    }

    private static string Flatten(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? "";
        if (el.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var item in el.EnumerateArray())
                parts.Add(item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? ""
                    : item.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "");
            return string.Join("\n", parts).Trim();
        }
        return el.GetRawText();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
