using System.Text.Json;

namespace HeadlessCoder;

/// <summary>
/// Persists user-chosen session titles (id → title) in a small JSON file, so
/// sessions can be renamed without touching the agents' own transcripts.
/// </summary>
public sealed class SessionTitleStore
{
    private readonly string _file;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _titles;

    public SessionTitleStore(string? path = null)
    {
        _file = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".headlesscoder", "titles.json");
        _titles = Load();
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_file));
                if (map is not null)
                    return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { /* start empty on any read/parse error */ }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string? Get(string id)
    {
        lock (_gate) return _titles.TryGetValue(id, out var t) ? t : null;
    }

    public IReadOnlyDictionary<string, string> Snapshot()
    {
        lock (_gate) return new Dictionary<string, string>(_titles, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Sets (or, when title is blank, clears) the custom title for a session.</summary>
    public void Set(string id, string? title)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(title))
                _titles.Remove(id);
            else
                _titles[id] = title.Trim().Length > 120 ? title.Trim()[..120] : title.Trim();
            Save();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_titles));
        }
        catch { /* best effort */ }
    }
}
