using HeadlessCoder.Agents;
using HeadlessCoder.Claude;

namespace HeadlessCoder.Tests;

public class AgentRegistryTests
{
    private static SessionSummary Session(string id, string cwd, DateTimeOffset? last, string provider) =>
        new(Id: id, ProjectId: "p", Cwd: cwd, Title: id, GitBranch: null,
            MessageCount: 1, LastActivity: last, Provider: provider);

    [Fact]
    public void Get_IsCaseInsensitive_AndReturnsNullForUnknown()
    {
        var reg = new AgentRegistry([new FakeAgentProvider("claude")]);

        Assert.NotNull(reg.Get("claude"));
        Assert.NotNull(reg.Get("CLAUDE"));
        Assert.Null(reg.Get("ghost"));
        Assert.Null(reg.Get(null));
    }

    [Fact]
    public void Providers_ExposesAllRegistered()
    {
        var reg = new AgentRegistry([new FakeAgentProvider("a"), new FakeAgentProvider("b")]);
        Assert.Equal(2, reg.Providers.Count);
    }

    [Fact]
    public void Diagnose_ReturnsDescriptorPerProvider()
    {
        var reg = new AgentRegistry([new FakeAgentProvider("a"), new FakeAgentProvider("b")]);

        var report = reg.Diagnose();

        Assert.Equal(2, report.Agents.Count);
        Assert.True(report.AnyAgentAvailable);
    }

    [Fact]
    public void Diagnose_CapturesDetectionFailure_AsMissingWithRemediation()
    {
        var bad = new FakeAgentProvider("bad") { DetectImpl = () => throw new InvalidOperationException("nope") };
        var reg = new AgentRegistry([bad]);

        var descriptor = Assert.Single(reg.Diagnose().Agents);

        Assert.False(descriptor.Installed);
        Assert.Equal("bad", descriptor.Id);
        Assert.Contains("Detection failed", descriptor.Remediation);
        Assert.Contains("nope", descriptor.Remediation);
    }

    [Fact]
    public void ListAllSessions_OrdersByLastActivityDescending()
    {
        var older = new FakeAgentProvider("a")
        {
            Sessions = [Session("old", "/x", DateTimeOffset.Parse("2024-01-01T00:00:00Z"), "a")],
        };
        var newer = new FakeAgentProvider("b")
        {
            Sessions = [Session("new", "/y", DateTimeOffset.Parse("2024-06-01T00:00:00Z"), "b")],
        };
        var reg = new AgentRegistry([older, newer]);

        var all = reg.ListAllSessions();

        Assert.Equal(["new", "old"], all.Select(s => s.Id));
    }

    [Fact]
    public void ListAllSessions_SwallowsAProviderThatThrows()
    {
        var good = new FakeAgentProvider("good")
        {
            Sessions = [Session("s", "/x", DateTimeOffset.UtcNow, "good")],
        };
        var bad = new FakeAgentProvider("bad") { ThrowOnListSessions = true };
        var reg = new AgentRegistry([bad, good]);

        var all = reg.ListAllSessions();

        Assert.Equal("s", Assert.Single(all).Id);
    }

    [Fact]
    public void ListWorkingDirectories_IsDistinctCaseInsensitive_AndSkipsBlank()
    {
        var provider = new FakeAgentProvider("a")
        {
            Sessions =
            [
                Session("1", "/home/proj", DateTimeOffset.UtcNow, "a"),
                Session("2", "/HOME/PROJ", DateTimeOffset.UtcNow, "a"), // same dir, different case
                Session("3", "/home/other", DateTimeOffset.UtcNow, "a"),
                Session("4", "   ", DateTimeOffset.UtcNow, "a"),          // blank -> skipped
            ],
        };
        var reg = new AgentRegistry([provider]);

        var dirs = reg.ListWorkingDirectories();

        Assert.Equal(2, dirs.Count);
        Assert.Contains("/home/other", dirs);
        Assert.Contains(dirs, d => d.Equals("/home/proj", StringComparison.OrdinalIgnoreCase));
    }
}
