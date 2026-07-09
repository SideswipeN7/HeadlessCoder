using System.Text;
using System.Text.Json;

namespace HeadlessCoder.Claude;

/// <summary>
/// Reads Claude Code's on-disk session transcripts from ~/.claude/projects.
/// Each project is a directory; each session is a &lt;id&gt;.jsonl file of newline
/// delimited events.
/// </summary>
public sealed class ClaudeSessionStore
{
    private readonly string _projectsRoot;

    public ClaudeSessionStore(string? projectsRoot = null)
    {
        _projectsRoot = projectsRoot
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "projects");
    }

    public string ProjectsRoot => _projectsRoot;

    public bool IsAvailable => Directory.Exists(_projectsRoot);

    public IReadOnlyList<ProjectInfo> ListProjects()
    {
        var result = new List<ProjectInfo>();
        if (!Directory.Exists(_projectsRoot))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(_projectsRoot))
        {
            var files = SafeEnumerateSessionFiles(dir);
            if (files.Count == 0)
                continue;

            string cwd = ReadCwd(files) ?? DecodeProjectId(Path.GetFileName(dir));
            DateTimeOffset? last = files.Max(f => (DateTimeOffset?)File.GetLastWriteTimeUtc(f));

            result.Add(new ProjectInfo(
                Id: Path.GetFileName(dir),
                Path: cwd,
                SessionCount: files.Count,
                LastActivity: last));
        }

        return result
            .OrderByDescending(p => p.LastActivity ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public IReadOnlyList<SessionSummary> ListSessions(string? projectId = null)
    {
        var result = new List<SessionSummary>();
        if (!Directory.Exists(_projectsRoot))
            return result;

        IEnumerable<string> dirs = projectId is null
            ? Directory.EnumerateDirectories(_projectsRoot)
            : new[] { Path.Combine(_projectsRoot, projectId) };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            string pid = Path.GetFileName(dir);

            foreach (var file in SafeEnumerateSessionFiles(dir))
            {
                var summary = TrySummarize(file, pid);
                if (summary is not null)
                    result.Add(summary);
            }
        }

        return result
            .OrderByDescending(s => s.LastActivity ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId, int maxMessages = 500)
    {
        string file = Path.Combine(_projectsRoot, projectId, sessionId + ".jsonl");
        var messages = new List<TranscriptMessage>();
        if (!File.Exists(file))
            return messages;

        foreach (var line in ReadLinesSafe(file))
        {
            if (line.Length == 0) continue;
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(line);
                root = doc.RootElement.Clone();
            }
            catch { continue; }

            if (!root.TryGetProperty("type", out var typeEl))
                continue;
            string type = typeEl.GetString() ?? "";
            if (type is not ("user" or "assistant"))
                continue;

            DateTimeOffset? ts = TryTimestamp(root);

            if (!root.TryGetProperty("message", out var msg))
                continue;

            foreach (var m in RenderMessage(type, msg, ts))
                messages.Add(m);
        }

        if (messages.Count > maxMessages)
            messages = messages.Skip(messages.Count - maxMessages).ToList();
        return messages;
    }

    /// <summary>
    /// Reads the last assistant turn's usage so the UI can show context/usage the
    /// moment a session is opened, without waiting for a new request.
    /// </summary>
    public AgentUsage? GetLastUsage(string projectId, string sessionId)
    {
        string file = Path.Combine(_projectsRoot, projectId, sessionId + ".jsonl");
        if (!File.Exists(file)) return null;

        AgentUsage? last = null;
        foreach (var line in ReadLinesSafe(file))
        {
            if (line.Length == 0) continue;
            JsonElement root;
            try { using var doc = JsonDocument.Parse(line); root = doc.RootElement.Clone(); }
            catch { continue; }

            if (!root.TryGetProperty("type", out var t) || t.GetString() != "assistant") continue;
            if (!root.TryGetProperty("message", out var m) ||
                !m.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) continue;

            long? inTok = GetLong(u, "input_tokens"), outTok = GetLong(u, "output_tokens");
            long? cr = GetLong(u, "cache_read_input_tokens"), cc = GetLong(u, "cache_creation_input_tokens");
            long ctx = (inTok ?? 0) + (cr ?? 0) + (cc ?? 0);
            if (ctx == 0) continue;

            string? model = m.TryGetProperty("model", out var mo) ? mo.GetString() : null;
            // The stored turn doesn't record the window; infer it (200k default, 1M when exceeded).
            long window = ctx > 200_000 ? 1_000_000 : 200_000;
            last = new AgentUsage(inTok, outTok, cr, cc, ctx, window, model);
        }
        return last;
    }

    private static long? GetLong(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;

    /// <summary>
    /// Deletes a session's transcript (the &lt;id&gt;.jsonl file and its sidecar
    /// folder) wherever it lives under the projects root. Used to keep in-private
    /// sessions out of history. Returns true if anything was removed.
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        if (!Directory.Exists(_projectsRoot) || !IsSafeId(sessionId))
            return false;

        bool removed = false;
        foreach (var dir in Directory.EnumerateDirectories(_projectsRoot))
        {
            try
            {
                string file = Path.Combine(dir, sessionId + ".jsonl");
                if (File.Exists(file)) { File.Delete(file); removed = true; }

                string sidecar = Path.Combine(dir, sessionId);
                if (Directory.Exists(sidecar)) { Directory.Delete(sidecar, recursive: true); removed = true; }
            }
            catch { /* best effort */ }
        }
        return removed;
    }

    // Guard against path traversal — session ids are GUID-like.
    private static bool IsSafeId(string id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    // ---- helpers -----------------------------------------------------------

    private static List<string> SafeEnumerateSessionFiles(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private SessionSummary? TrySummarize(string file, string projectId)
    {
        string id = Path.GetFileNameWithoutExtension(file);
        string? cwd = null;
        string? branch = null;
        string? title = null;
        int messageCount = 0;
        DateTimeOffset? last = null;

        foreach (var line in ReadLinesSafe(file))
        {
            if (line.Length == 0) continue;
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(line);
                root = doc.RootElement.Clone();
            }
            catch { continue; }

            if (root.TryGetProperty("cwd", out var cwdEl) && cwd is null)
                cwd = cwdEl.GetString();
            if (root.TryGetProperty("gitBranch", out var brEl) && branch is null)
                branch = brEl.GetString();

            if (root.TryGetProperty("type", out var typeEl))
            {
                string type = typeEl.GetString() ?? "";
                if (type is "user" or "assistant")
                {
                    messageCount++;
                    var ts = TryTimestamp(root);
                    if (ts is not null) last = ts;

                    if (title is null && type == "user" && IsHumanText(root, out string text))
                        title = text;
                }
            }
        }

        if (messageCount == 0)
            return null;

        last ??= File.GetLastWriteTimeUtc(file);
        cwd ??= DecodeProjectId(projectId);
        title = string.IsNullOrWhiteSpace(title) ? "(untitled session)" : Truncate(title!, 80);

        return new SessionSummary(id, projectId, cwd, title, branch, messageCount, last);
    }

    private static bool IsHumanText(JsonElement root, out string text)
    {
        text = "";
        // Skip tool-result / meta user turns; we want a real typed prompt.
        if (root.TryGetProperty("origin", out var origin) &&
            origin.ValueKind == JsonValueKind.Object &&
            origin.TryGetProperty("kind", out var kind) &&
            kind.GetString() is string k && k != "human")
            return false;

        if (!root.TryGetProperty("message", out var msg) ||
            !msg.TryGetProperty("content", out var content))
            return false;

        if (content.ValueKind == JsonValueKind.String)
        {
            text = content.GetString() ?? "";
            return text.Length > 0 && !text.StartsWith("<");
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var bt) && bt.GetString() == "text" &&
                    block.TryGetProperty("text", out var txt))
                    sb.Append(txt.GetString());
            }
            text = sb.ToString().Trim();
            return text.Length > 0 && !text.StartsWith("<");
        }

        return false;
    }

    private static IEnumerable<TranscriptMessage> RenderMessage(string type, JsonElement msg, DateTimeOffset? ts)
    {
        if (!msg.TryGetProperty("content", out var content))
            yield break;

        if (content.ValueKind == JsonValueKind.String)
        {
            string s = content.GetString() ?? "";
            if (s.Length > 0)
                yield return new TranscriptMessage(type, s, ts);
            yield break;
        }

        if (content.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var block in content.EnumerateArray())
        {
            if (!block.TryGetProperty("type", out var bt))
                continue;
            switch (bt.GetString())
            {
                case "text":
                    if (block.TryGetProperty("text", out var txt))
                    {
                        string s = txt.GetString() ?? "";
                        if (s.Trim().Length > 0)
                            yield return new TranscriptMessage(type, s, ts);
                    }
                    break;
                case "tool_use":
                    string name = block.TryGetProperty("name", out var n) ? n.GetString() ?? "tool" : "tool";
                    string input = block.TryGetProperty("input", out var inp) ? Summarize(inp) : "";
                    yield return new TranscriptMessage("tool", input, ts, name);
                    break;
                case "tool_result":
                    string res = block.TryGetProperty("content", out var rc) ? Summarize(rc) : "";
                    if (res.Trim().Length > 0)
                        yield return new TranscriptMessage("tool", Truncate(res, 4000), ts, "result");
                    break;
            }
        }
    }

    private static string Summarize(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                return el.GetString() ?? "";
            case JsonValueKind.Array:
                var sb = new StringBuilder();
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        sb.AppendLine(item.GetString());
                    else if (item.TryGetProperty("text", out var t))
                        sb.AppendLine(t.GetString());
                }
                return sb.ToString().Trim();
            case JsonValueKind.Object:
                return el.GetRawText();
            default:
                return el.ToString();
        }
    }

    private static string? ReadCwd(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            foreach (var line in ReadLinesSafe(file))
            {
                if (!line.Contains("\"cwd\"")) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("cwd", out var cwd))
                    {
                        string? v = cwd.GetString();
                        if (!string.IsNullOrWhiteSpace(v))
                            return v;
                    }
                }
                catch { /* keep scanning */ }
            }
        }
        return null;
    }

    private static DateTimeOffset? TryTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(ts.GetString(), out var parsed))
            return parsed;
        return null;
    }

    /// <summary>
    /// Best-effort reverse of Claude's directory encoding (E:\Repository\Foo -> E--Repository-Foo).
    /// The encoding is lossy, so real cwd is preferred from file content when available.
    /// </summary>
    private static string DecodeProjectId(string id) => id;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static IEnumerable<string> ReadLinesSafe(string file)
    {
        StreamReader? reader = null;
        try
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            reader = new StreamReader(stream, Encoding.UTF8);
        }
        catch
        {
            yield break;
        }

        using (reader)
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
                yield return line;
        }
    }
}
