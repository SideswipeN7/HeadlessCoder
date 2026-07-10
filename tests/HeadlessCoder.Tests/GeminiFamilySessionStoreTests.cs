using HeadlessCoder.Agents;

namespace HeadlessCoder.Tests;

public class GeminiFamilySessionStoreTests : IDisposable
{
    private readonly string _root;

    public GeminiFamilySessionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "hc-gem-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private string WriteSession(string projectId, string sessionFile, string? projectRoot, params string[] lines)
    {
        string projDir = Path.Combine(_root, projectId);
        string chats = Path.Combine(projDir, "chats");
        Directory.CreateDirectory(chats);
        if (projectRoot is not null)
            File.WriteAllText(Path.Combine(projDir, ".project_root"), projectRoot);
        string path = Path.Combine(chats, sessionFile);
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void IsAvailable_ReflectsDirectoryExistence()
    {
        var missing = new GeminiFamilySessionStore(_root);
        Assert.False(missing.IsAvailable);
        Assert.Equal(_root, missing.StorePath);

        Directory.CreateDirectory(_root);
        Assert.True(new GeminiFamilySessionStore(_root).IsAvailable);
    }

    [Fact]
    public void ListSessions_ReturnsEmpty_WhenRootMissing()
    {
        var store = new GeminiFamilySessionStore(_root);
        Assert.Empty(store.ListSessions("qwen"));
    }

    [Fact]
    public void ListSessions_SummarizesSession()
    {
        WriteSession("proj1", "session-1.jsonl", @"C:\work\proj1",
            "{\"sessionId\":\"sess-1\",\"lastUpdated\":\"2024-01-01T10:00:00Z\"}",
            "{\"type\":\"user\",\"id\":\"m1\",\"content\":[{\"text\":\"Hello world\"}],\"timestamp\":\"2024-01-01T10:01:00Z\"}",
            "{\"type\":\"gemini\",\"id\":\"m2\",\"content\":\"Hi there\",\"timestamp\":\"2024-01-01T10:02:00Z\"}");

        var store = new GeminiFamilySessionStore(_root);
        var session = Assert.Single(store.ListSessions("qwen"));

        Assert.Equal("sess-1", session.Id);
        Assert.Equal("proj1", session.ProjectId);
        Assert.Equal(@"C:\work\proj1", session.Cwd);
        Assert.Equal("Hello world", session.Title);
        Assert.Equal(2, session.MessageCount);
        Assert.Equal("qwen", session.Provider);
        Assert.NotNull(session.LastActivity);
    }

    [Fact]
    public void ListSessions_SkipsMetadataOnlySessions()
    {
        WriteSession("proj-empty", "session-9.jsonl", null,
            "{\"sessionId\":\"empty\",\"lastUpdated\":\"2024-01-01T10:00:00Z\"}",
            "{\"$set\":{\"lastUpdated\":\"2024-01-01T10:05:00Z\"}}");

        var store = new GeminiFamilySessionStore(_root);
        Assert.Empty(store.ListSessions("qwen"));
    }

    [Fact]
    public void ListSessions_TruncatesLongTitle()
    {
        string longText = new string('a', 200);
        WriteSession("proj2", "session-2.jsonl", null,
            "{\"sessionId\":\"sess-2\"}",
            "{\"type\":\"user\",\"id\":\"m1\",\"content\":[{\"text\":\"" + longText + "\"}]}");

        var store = new GeminiFamilySessionStore(_root);
        var session = Assert.Single(store.ListSessions("qwen"));

        Assert.EndsWith("…", session.Title);
        Assert.Equal(81, session.Title.Length); // 80 chars + ellipsis
    }

    [Fact]
    public void GetTranscript_ReturnsMessages_InOrder_WithRolesMapped()
    {
        WriteSession("proj1", "session-1.jsonl", @"C:\work\proj1",
            "{\"sessionId\":\"sess-1\"}",
            "{\"type\":\"user\",\"id\":\"m1\",\"content\":[{\"text\":\"Question?\"}]}",
            "{\"type\":\"gemini\",\"id\":\"m2\",\"content\":\"Answer.\"}");

        var store = new GeminiFamilySessionStore(_root);
        var messages = store.GetTranscript("proj1", "sess-1");

        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("Question?", messages[0].Text);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("Answer.", messages[1].Text);
    }

    [Fact]
    public void GetTranscript_KeepsLatestSnapshotPerMessageId()
    {
        WriteSession("proj1", "session-1.jsonl", null,
            "{\"sessionId\":\"sess-1\"}",
            "{\"type\":\"user\",\"id\":\"m1\",\"content\":[{\"text\":\"Question?\"}]}",
            "{\"type\":\"gemini\",\"id\":\"m2\",\"content\":\"partial\"}",
            "{\"type\":\"gemini\",\"id\":\"m2\",\"content\":\"final answer\"}");

        var store = new GeminiFamilySessionStore(_root);
        var messages = store.GetTranscript("proj1", "sess-1");

        Assert.Equal(2, messages.Count);
        Assert.Equal("final answer", messages[1].Text);
    }

    [Fact]
    public void GetTranscript_SkipsThoughtsOnlySnapshots()
    {
        WriteSession("proj1", "session-1.jsonl", null,
            "{\"sessionId\":\"sess-1\"}",
            "{\"type\":\"gemini\",\"id\":\"m2\",\"content\":\"\"}",           // empty -> skipped
            "{\"type\":\"user\",\"id\":\"m1\",\"content\":[{\"text\":\"Hi\"}]}");

        var store = new GeminiFamilySessionStore(_root);
        var messages = store.GetTranscript("proj1", "sess-1");

        Assert.Single(messages);
        Assert.Equal("Hi", messages[0].Text);
    }

    [Fact]
    public void GetTranscript_ReturnsEmpty_ForUnknownSession()
    {
        WriteSession("proj1", "session-1.jsonl", null, "{\"sessionId\":\"sess-1\"}");
        var store = new GeminiFamilySessionStore(_root);

        Assert.Empty(store.GetTranscript("proj1", "does-not-exist"));
        Assert.Empty(store.GetTranscript("no-such-project", "sess-1"));
    }

    [Fact]
    public void Store_IgnoresMalformedJsonLines()
    {
        WriteSession("proj1", "session-1.jsonl", null,
            "{\"sessionId\":\"sess-1\"}",
            "this is not json",
            "{\"type\":\"user\",\"id\":\"m1\",\"content\":[{\"text\":\"still works\"}]}");

        var store = new GeminiFamilySessionStore(_root);

        Assert.Single(store.ListSessions("qwen"));
        Assert.Equal("still works", Assert.Single(store.GetTranscript("proj1", "sess-1")).Text);
    }
}
