using System.Text.Json;
using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Reads past sessions/transcripts for a CLI whose on-disk store we understand.
/// Providers that support history expose one of these; those that don't return null.
/// </summary>
public interface ICliHistoryStore
{
    /// <summary>Root directory the store lives in (shown by the doctor); null if absent.</summary>
    string? StorePath { get; }

    /// <summary>True when the store directory actually exists on disk.</summary>
    bool IsAvailable { get; }

    IReadOnlyList<SessionSummary> ListSessions(string providerId);
    IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId);
}

/// <summary>
/// History reader for the Gemini-CLI family of tools (Gemini CLI, Qwen Code, and
/// other forks). Sessions live under <c>&lt;root&gt;/&lt;project&gt;/chats/session-*.jsonl</c>;
/// the first line is session metadata, following lines are message snapshots
/// (deduplicated by id, keeping the latest) interleaved with <c>{"$set":…}</c> ops.
/// The real working directory is stored in a <c>.project_root</c> file per project.
/// </summary>
public sealed class GeminiFamilySessionStore : ICliHistoryStore
{
    private readonly string _root;

    /// <param name="tmpRoot">e.g. ~/.qwen/tmp — each child dir is a project.</param>
    public GeminiFamilySessionStore(string tmpRoot) => _root = tmpRoot;

    public string? StorePath => _root;
    public bool IsAvailable => Directory.Exists(_root);

    public IReadOnlyList<SessionSummary> ListSessions(string providerId)
    {
        var sessions = new List<SessionSummary>();
        if (!Directory.Exists(_root)) return sessions;

        foreach (var projectDir in Directory.EnumerateDirectories(_root))
        {
            string projectId = Path.GetFileName(projectDir);
            string cwd = ReadProjectRoot(projectDir);
            string chats = Path.Combine(projectDir, "chats");
            if (!Directory.Exists(chats)) continue;

            foreach (var file in Directory.EnumerateFiles(chats, "session-*.jsonl"))
            {
                var s = TrySummarize(file, projectId, cwd, providerId);
                if (s is not null) sessions.Add(s);
            }
        }

        return sessions
            .OrderByDescending(s => s.LastActivity ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId)
    {
        var messages = new List<TranscriptMessage>();
        string chats = Path.Combine(_root, projectId, "chats");
        if (!Directory.Exists(chats)) return messages;

        string? file = FindSessionFile(chats, sessionId);
        if (file is null) return messages;

        // Keep only the latest snapshot per message id, in first-seen order.
        var order = new List<string>();
        var latest = new Dictionary<string, TranscriptMessage>();

        foreach (var line in ReadLinesSafe(file))
        {
            if (line.Length == 0) continue;
            JsonElement el;
            try { using var doc = JsonDocument.Parse(line); el = doc.RootElement.Clone(); }
            catch { continue; }
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (el.TryGetProperty("$set", out _)) continue;                 // mutation op
            if (!el.TryGetProperty("type", out var typeEl)) continue;       // metadata / other

            string type = typeEl.GetString() ?? "";
            string role = type switch
            {
                "user" => "user",
                "gemini" or "assistant" or "model" => "assistant",
                _ => "",
            };
            if (role.Length == 0) continue;

            string text = ExtractText(el);
            if (text.Length == 0) continue;                                 // thoughts-only snapshot

            string id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            DateTimeOffset? ts = el.TryGetProperty("timestamp", out var tEl) &&
                                 tEl.TryGetDateTimeOffset(out var t) ? t : null;
            var msg = new TranscriptMessage(role, text, ts);

            string key = id.Length > 0 ? id : $"{order.Count}:{role}";
            if (!latest.ContainsKey(key)) order.Add(key);
            latest[key] = msg;
        }

        foreach (var key in order)
            messages.Add(latest[key]);
        return messages;
    }

    // ---- helpers ------------------------------------------------------------

    private static SessionSummary? TrySummarize(
        string file, string projectId, string cwd, string providerId)
    {
        string? sessionId = null;
        DateTimeOffset? lastActivity = null;
        string title = "";
        int messageCount = 0;

        foreach (var line in ReadLinesSafe(file))
        {
            if (line.Length == 0) continue;
            JsonElement el;
            try { using var doc = JsonDocument.Parse(line); el = doc.RootElement.Clone(); }
            catch { continue; }
            if (el.ValueKind != JsonValueKind.Object) continue;

            if (sessionId is null && el.TryGetProperty("sessionId", out var sidEl))
            {
                sessionId = sidEl.GetString();
                if (el.TryGetProperty("lastUpdated", out var luEl) &&
                    luEl.TryGetDateTimeOffset(out var lu)) lastActivity = lu;
                continue;
            }
            if (el.TryGetProperty("$set", out var setEl))
            {
                if (setEl.ValueKind == JsonValueKind.Object &&
                    setEl.TryGetProperty("lastUpdated", out var luEl) &&
                    luEl.TryGetDateTimeOffset(out var lu)) lastActivity = lu;
                continue;
            }
            if (!el.TryGetProperty("type", out var typeEl)) continue;
            string type = typeEl.GetString() ?? "";
            if (type is not ("user" or "gemini" or "assistant" or "model")) continue;

            messageCount++;
            if (title.Length == 0 && type == "user")
            {
                string t = ExtractText(el).Replace('\n', ' ').Trim();
                if (t.Length > 0) title = t.Length > 80 ? t[..80] + "…" : t;
            }
            if (el.TryGetProperty("timestamp", out var tsEl) &&
                tsEl.TryGetDateTimeOffset(out var ts))
            {
                if (lastActivity is null || ts > lastActivity) lastActivity = ts;
            }
        }

        if (sessionId is null) return null;
        if (title.Length == 0) title = "(untitled session)";
        // Skip empty sessions (metadata only, no messages).
        if (messageCount == 0) return null;

        return new SessionSummary(
            Id: sessionId,
            ProjectId: projectId,
            Cwd: cwd,
            Title: title,
            GitBranch: null,
            MessageCount: messageCount,
            LastActivity: lastActivity,
            Provider: providerId);
    }

    // Message content is either a string (assistant) or an array of {text} parts (user).
    private static string ExtractText(JsonElement el)
    {
        if (!el.TryGetProperty("content", out var c)) return "";
        if (c.ValueKind == JsonValueKind.String) return c.GetString()?.Trim() ?? "";
        if (c.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var part in c.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String) parts.Add(part.GetString() ?? "");
                else if (part.ValueKind == JsonValueKind.Object &&
                         part.TryGetProperty("text", out var tx) &&
                         tx.ValueKind == JsonValueKind.String) parts.Add(tx.GetString() ?? "");
            }
            return string.Join("", parts).Trim();
        }
        return "";
    }

    private static string? FindSessionFile(string chatsDir, string sessionId)
    {
        foreach (var file in Directory.EnumerateFiles(chatsDir, "session-*.jsonl"))
        {
            foreach (var line in ReadLinesSafe(file))
            {
                if (line.Length == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("sessionId", out var sidEl) &&
                        sidEl.GetString() == sessionId)
                        return file;
                }
                catch { /* keep scanning */ }
                break; // sessionId lives on the first line only
            }
        }
        return null;
    }

    private static string ReadProjectRoot(string projectDir)
    {
        try
        {
            string marker = Path.Combine(projectDir, ".project_root");
            if (File.Exists(marker)) return File.ReadAllText(marker).Trim();
        }
        catch { /* ignore */ }
        return "";
    }

    private static IEnumerable<string> ReadLinesSafe(string file)
    {
        StreamReader reader;
        try
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            reader = new StreamReader(fs);
        }
        catch { yield break; }
        using (reader)
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
                yield return line;
        }
    }
}
