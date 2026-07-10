namespace HeadlessCoder.Tests;

public class SessionTitleStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public SessionTitleStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "titles.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownId()
    {
        var store = new SessionTitleStore(_file);
        Assert.Null(store.Get("nope"));
    }

    [Fact]
    public void Set_ThenGet_ReturnsTitle()
    {
        var store = new SessionTitleStore(_file);
        store.Set("s1", "My session");
        Assert.Equal("My session", store.Get("s1"));
    }

    [Fact]
    public void Set_TrimsWhitespace()
    {
        var store = new SessionTitleStore(_file);
        store.Set("s1", "   spaced   ");
        Assert.Equal("spaced", store.Get("s1"));
    }

    [Fact]
    public void Set_TruncatesTo120Characters()
    {
        var store = new SessionTitleStore(_file);
        store.Set("s1", new string('x', 500));
        Assert.Equal(120, store.Get("s1")!.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_BlankTitle_ClearsExistingEntry(string? blank)
    {
        var store = new SessionTitleStore(_file);
        store.Set("s1", "something");
        store.Set("s1", blank);
        Assert.Null(store.Get("s1"));
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var store = new SessionTitleStore(_file);
        store.Set("AbC", "value");
        Assert.Equal("value", store.Get("abc"));
    }

    [Fact]
    public void Titles_PersistAcrossInstances()
    {
        new SessionTitleStore(_file).Set("s1", "persisted");

        var reloaded = new SessionTitleStore(_file);
        Assert.Equal("persisted", reloaded.Get("s1"));
    }

    [Fact]
    public void Snapshot_ContainsAllTitles_AndIsDecoupledFromStore()
    {
        var store = new SessionTitleStore(_file);
        store.Set("a", "A");
        store.Set("b", "B");

        var snap = store.Snapshot();
        Assert.Equal(2, snap.Count);
        Assert.Equal("A", snap["a"]);

        // Mutating the store afterwards must not change the earlier snapshot.
        store.Set("c", "C");
        Assert.Equal(2, snap.Count);
    }

    [Fact]
    public void Load_StartsEmpty_WhenFileIsCorrupt()
    {
        File.WriteAllText(_file, "{ this is not valid json ");
        var store = new SessionTitleStore(_file);
        Assert.Null(store.Get("anything"));
        Assert.Empty(store.Snapshot());
    }
}
