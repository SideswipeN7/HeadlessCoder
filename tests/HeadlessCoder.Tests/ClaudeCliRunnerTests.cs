using HeadlessCoder.Claude;

namespace HeadlessCoder.Tests;

public class ClaudeCliRunnerTests
{
    private static SendMessageRequest Req(Action<SendMessageRequest>? configure = null)
    {
        var r = new SendMessageRequest { Message = "hello", Cwd = "" };
        configure?.Invoke(r);
        return r;
    }

    private static string? ValueAfter(IReadOnlyList<string> args, string flag)
    {
        int i = args.ToList().IndexOf(flag);
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    [Fact]
    public void BuildArgs_AlwaysUsesHeadlessStreamingFlags()
    {
        var args = ClaudeCliRunner.BuildArgs(Req());

        Assert.Equal("hello", ValueAfter(args, "--print"));
        Assert.Equal("stream-json", ValueAfter(args, "--output-format"));
        Assert.Contains("--include-partial-messages", args);
        Assert.Contains("--verbose", args);
    }

    [Fact]
    public void BuildArgs_NewSession_UsesSessionIdFlag_WithProvidedId()
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r =>
        {
            r.IsNewSession = true;
            r.SessionId = "abc-123";
        }));

        Assert.Equal("abc-123", ValueAfter(args, "--session-id"));
        Assert.DoesNotContain("--resume", args);
    }

    [Fact]
    public void BuildArgs_ExistingSession_UsesResumeFlag()
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r =>
        {
            r.IsNewSession = false;
            r.SessionId = "existing-99";
        }));

        Assert.Equal("existing-99", ValueAfter(args, "--resume"));
        Assert.DoesNotContain("--session-id", args);
    }

    [Fact]
    public void BuildArgs_MissingSessionId_TreatedAsNewSession_WithGeneratedId()
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r => r.SessionId = null));

        string? id = ValueAfter(args, "--session-id");
        Assert.True(Guid.TryParse(id, out _));
    }

    [Theory]
    [InlineData("acceptEdits", "acceptEdits")]
    [InlineData("plan", "plan")]
    [InlineData("bypassPermissions", "bypassPermissions")]
    [InlineData("default", "default")]
    [InlineData("garbage", "default")]   // unknown falls back to default
    [InlineData("", "default")]
    public void BuildArgs_NormalizesPermissionMode(string input, string expected)
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r => r.PermissionMode = input));
        Assert.Equal(expected, ValueAfter(args, "--permission-mode"));
    }

    [Fact]
    public void BuildArgs_IncludesModel_WhenProvided()
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r => r.Model = "claude-opus-4-8"));
        Assert.Equal("claude-opus-4-8", ValueAfter(args, "--model"));
    }

    [Fact]
    public void BuildArgs_OmitsModel_WhenBlank()
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r => r.Model = "  "));
        Assert.DoesNotContain("--model", args);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("xhigh")]
    [InlineData("max")]
    public void BuildArgs_IncludesValidEffort(string effort)
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r => r.Effort = effort));
        Assert.Equal(effort, ValueAfter(args, "--effort"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ultra")]
    public void BuildArgs_OmitsInvalidEffort(string? effort)
    {
        var args = ClaudeCliRunner.BuildArgs(Req(r => r.Effort = effort));
        Assert.DoesNotContain("--effort", args);
    }

    [Fact]
    public void JsonEscape_EscapesBackslashesQuotesAndNewlines_AndStripsCarriageReturns()
    {
        string escaped = ClaudeCliRunner.JsonEscape("a\\b\"c\r\nd");
        Assert.Equal("a\\\\b\\\"c\\nd", escaped);
    }

    [Fact]
    public void Executable_IsNonEmpty()
    {
        // Constructing the runner probes for the CLI but never spawns it.
        Assert.False(string.IsNullOrWhiteSpace(new ClaudeCliRunner().Executable));
        Assert.Equal("/custom/claude", new ClaudeCliRunner("/custom/claude").Executable);
    }
}
