using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Central registry of agent providers. Runs detection, aggregates sessions
/// across every installed agent, and routes messages to the right provider.
/// </summary>
public sealed class AgentRegistry
{
    private readonly IReadOnlyList<IAgentProvider> _providers;
    private readonly Dictionary<string, IAgentProvider> _byId;

    public AgentRegistry(IEnumerable<IAgentProvider> providers)
    {
        _providers = providers.ToList();
        _byId = _providers.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IAgentProvider> Providers => _providers;

    public IAgentProvider? Get(string? id) =>
        id is not null && _byId.TryGetValue(id, out var p) ? p : null;

    /// <summary>Runs detection across all providers (used by the doctor + UI).</summary>
    public DoctorReport Diagnose()
    {
        var agents = new List<AgentDescriptor>();
        foreach (var p in _providers)
        {
            try { agents.Add(p.Detect()); }
            catch (Exception ex)
            {
                agents.Add(new AgentDescriptor
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    Installed = false,
                    Remediation = "Detection failed: " + ex.Message,
                });
            }
        }
        return new DoctorReport { Agents = agents };
    }

    /// <summary>All sessions from every provider that supports history.</summary>
    public IReadOnlyList<SessionSummary> ListAllSessions()
    {
        var all = new List<SessionSummary>();
        foreach (var p in _providers)
        {
            try { all.AddRange(p.ListSessions()); }
            catch { /* one bad provider shouldn't break the list */ }
        }
        return all
            .OrderByDescending(s => s.LastActivity ?? DateTimeOffset.MinValue)
            .ToList();
    }

    /// <summary>Distinct working directories seen across all sessions (for the new-session picker).</summary>
    public IReadOnlyList<string> ListWorkingDirectories() =>
        ListAllSessions()
            .Select(s => s.Cwd)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
