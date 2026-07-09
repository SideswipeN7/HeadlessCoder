using System.Globalization;
using System.Text.Json;
using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Reads GitHub Copilot CLI sessions. Each session is a directory under
/// <c>~/.copilot/session-state/&lt;id&gt;/</c> holding an <c>events.jsonl</c> transcript
/// (typed events: session.start, user.message, assistant.message, …) and a
/// <c>workspace.yaml</c> with the title, cwd and timestamps.
/// </summary>
public sealed class CopilotSessionStore : ICliHistoryStore
{
    private readonly string _root;

    public CopilotSessionStore(string? sessionStateRoot = null) =>
        _root = sessionStateRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "session-state");

    public string? StorePath => _root;
    public bool IsAvailable => Directory.Exists(_root);

    public IReadOnlyList<SessionSummary> ListSessions(string providerId)
    {
        var sessions = new List<SessionSummary>();
        if (!Directory.Exists(_root)) return sessions;

        foreach (var dir in Directory.EnumerateDirectories(_root))
        {
            string id = Path.GetFileName(dir);
            string events = Path.Combine(dir, "events.jsonl");
            if (!File.Exists(events)) continue;

            var meta = ReadWorkspace(Path.Combine(dir, "workspace.yaml"));
            int count = CountMessages(events);
            if (count == 0) continue;

            meta.TryGetValue("name", out var title);
            meta.TryGetValue("cwd", out var cwd);
            meta.TryGetValue("updated_at", out var updated);
            if (string.IsNullOrWhiteSpace(title)) title = "(untitled session)";

            sessions.Add(new SessionSummary(
                Id: id,
                ProjectId: "session-state",
                Cwd: cwd ?? "",
                Title: title!,
                GitBranch: null,
                MessageCount: count,
                LastActivity: ParseDate(updated),
                Provider: providerId));
        }

        return sessions
            .OrderByDescending(s => s.LastActivity ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId)
    {
        var messages = new List<TranscriptMessage>();
        string events = Path.Combine(_root, sessionId, "events.jsonl");
        if (!File.Exists(events)) return messages;

        foreach (var line in ReadLinesSafe(events))
        {
            if (line.Length == 0) continue;
            JsonElement el;
            try { using var doc = JsonDocument.Parse(line); el = doc.RootElement.Clone(); }
            catch { continue; }

            string type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            string role = type switch
            {
                "user.message" => "user",
                "assistant.message" => "assistant",
                _ => "",
            };
            if (role.Length == 0) continue;
            if (!el.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) continue;

            string text = data.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() ?? "" : "";
            if (text.Trim().Length == 0) continue;

            DateTimeOffset? ts = el.TryGetProperty("timestamp", out var tsEl) &&
                                 tsEl.TryGetDateTimeOffset(out var d) ? d : null;
            messages.Add(new TranscriptMessage(role, text.Trim(), ts));
        }
        return messages;
    }

    // ---- helpers ------------------------------------------------------------

    private static int CountMessages(string eventsFile)
    {
        int n = 0;
        foreach (var line in ReadLinesSafe(eventsFile))
        {
            if (line.Contains("\"user.message\"") || line.Contains("\"assistant.message\"")) n++;
        }
        return n;
    }

    // Minimal flat-YAML reader (key: value per line) — enough for workspace.yaml.
    private static Dictionary<string, string> ReadWorkspace(string file)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in ReadLinesSafe(file))
        {
            int i = line.IndexOf(':');
            if (i <= 0) continue;
            string key = line[..i].Trim();
            string val = line[(i + 1)..].Trim().Trim('"', '\'');
            if (key.Length > 0) map[key] = val;
        }
        return map;
    }

    private static DateTimeOffset? ParseDate(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var d) ? d : null;

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
