using HeadlessCoder.Agents;

namespace HeadlessCoder.Tests;

public class AgentModelsTests
{
    [Fact]
    public void AgentEvent_Factories_SetKindAndPayload()
    {
        Assert.Equal("system", AgentEvent.System("sid").Kind);
        Assert.Equal("sid", AgentEvent.System("sid").SessionId);

        var td = AgentEvent.TextDelta("hi");
        Assert.Equal("text_delta", td.Kind);
        Assert.Equal("hi", td.Text);

        var a = AgentEvent.Assistant("done");
        Assert.Equal("assistant", a.Kind);
        Assert.Equal("done", a.Text);

        var tool = AgentEvent.Tool("Read", "file.cs");
        Assert.Equal("tool", tool.Kind);
        Assert.Equal("Read", tool.ToolName);
        Assert.Equal("file.cs", tool.Text);

        var tr = AgentEvent.ToolResult("Read", "ok");
        Assert.Equal("tool_result", tr.Kind);
        Assert.Equal("Read", tr.ToolName);

        var err = AgentEvent.Error("bad");
        Assert.Equal("error", err.Kind);
        Assert.Equal("bad", err.Message);
    }

    [Fact]
    public void AgentDescriptor_Status_IsMissing_WhenNotInstalled()
    {
        var d = new AgentDescriptor { Id = "x", DisplayName = "X", Installed = false };
        Assert.Equal("missing", d.Status);
    }

    [Fact]
    public void AgentDescriptor_Status_IsPartial_WhenHistoryStoreMissing()
    {
        var d = new AgentDescriptor
        {
            Id = "x", DisplayName = "X",
            Installed = true, SupportsHistory = true, ConfigFound = false,
        };
        Assert.Equal("partial", d.Status);
    }

    [Fact]
    public void AgentDescriptor_Status_IsReady_WhenInstalledAndConfigured()
    {
        var d = new AgentDescriptor
        {
            Id = "x", DisplayName = "X",
            Installed = true, SupportsHistory = true, ConfigFound = true,
        };
        Assert.Equal("ready", d.Status);
    }

    [Fact]
    public void AgentDescriptor_Status_IsReady_WhenInstalledWithoutHistorySupport()
    {
        var d = new AgentDescriptor
        {
            Id = "x", DisplayName = "X",
            Installed = true, SupportsHistory = false, ConfigFound = false,
        };
        Assert.Equal("ready", d.Status);
    }

    [Fact]
    public void DoctorReport_AggregatesAvailabilityAndSessionCounts()
    {
        var report = new DoctorReport
        {
            Agents =
            [
                new AgentDescriptor { Id = "a", DisplayName = "A", Installed = true, SessionCount = 3 },
                new AgentDescriptor { Id = "b", DisplayName = "B", Installed = false, SessionCount = 0 },
            ],
        };

        Assert.True(report.AnyAgentAvailable);
        Assert.Equal(3, report.TotalSessions);
    }

    [Fact]
    public void DoctorReport_AnyAgentAvailable_IsFalse_WhenNoneInstalled()
    {
        var report = new DoctorReport
        {
            Agents = [new AgentDescriptor { Id = "a", DisplayName = "A", Installed = false }],
        };

        Assert.False(report.AnyAgentAvailable);
        Assert.Equal(0, report.TotalSessions);
    }
}
