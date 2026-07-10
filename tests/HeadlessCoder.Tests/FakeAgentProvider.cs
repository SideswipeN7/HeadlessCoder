using System.Runtime.CompilerServices;
using HeadlessCoder.Agents;
using HeadlessCoder.Claude;

namespace HeadlessCoder.Tests;

/// <summary>Configurable in-memory <see cref="IAgentProvider"/> for registry tests.</summary>
internal sealed class FakeAgentProvider : IAgentProvider
{
    public FakeAgentProvider(string id) => Id = id;

    public string Id { get; }
    public string DisplayName => Id + " CLI";

    public Func<AgentDescriptor>? DetectImpl { get; set; }
    public IReadOnlyList<SessionSummary> Sessions { get; set; } = Array.Empty<SessionSummary>();
    public bool ThrowOnListSessions { get; set; }

    public AgentDescriptor Detect() =>
        DetectImpl?.Invoke() ?? new AgentDescriptor
        {
            Id = Id,
            DisplayName = DisplayName,
            Installed = true,
            ConfigFound = true,
            SessionCount = Sessions.Count,
        };

    public IReadOnlyList<SessionSummary> ListSessions() =>
        ThrowOnListSessions ? throw new InvalidOperationException("boom") : Sessions;

    public IReadOnlyList<TranscriptMessage> GetTranscript(string projectId, string sessionId) =>
        Array.Empty<TranscriptMessage>();

    public async IAsyncEnumerable<AgentEvent> SendAsync(
        SendMessageRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }
}
