using HeadlessCoder.Hosting;

namespace HeadlessCoder.Tests;

public class SseTests
{
    [Fact]
    public void Frame_SingleLineData_ProducesOneDataLine()
    {
        string frame = Sse.Frame("done", "{}");

        Assert.Equal("event: done\ndata: {}\n\n", frame);
    }

    [Fact]
    public void Frame_AlwaysEndsWithBlankLine_SoClientCanSplitOnDoubleNewline()
    {
        // The browser parser buffers until it sees "\n\n"; every frame must end with it.
        string frame = Sse.Frame("agent", "{\"kind\":\"text_delta\"}");

        Assert.EndsWith("\n\n", frame);
        Assert.Single(SplitFrames(frame + Sse.Frame("done", "{}")), f => f.Contains("text_delta"));
    }

    [Fact]
    public void Frame_MultiLineData_EmitsOneDataLinePerLine()
    {
        // Pretty-printed JSON contains newlines; each must become its own data: line
        // so the whole payload is delivered as a single SSE event.
        string data = "{\n  \"a\": 1\n}";

        string frame = Sse.Frame("agent", data);

        Assert.Equal(
            "event: agent\ndata: {\ndata:   \"a\": 1\ndata: }\n\n",
            frame);
    }

    [Fact]
    public void Frame_ReassembledDataMatchesOriginal()
    {
        string data = "line-1\nline-2\nline-3";

        string frame = Sse.Frame("line", data);

        // Mirror the client: strip the "data: " prefix and rejoin with newlines.
        var dataLines = frame
            .Split('\n')
            .Where(l => l.StartsWith("data: "))
            .Select(l => l["data: ".Length..]);
        Assert.Equal(data, string.Join("\n", dataLines));
    }

    [Theory]
    [InlineData("meta")]
    [InlineData("agent")]
    [InlineData("line")]
    [InlineData("done")]
    public void Frame_StartsWithEventName(string ev)
    {
        Assert.StartsWith($"event: {ev}\n", Sse.Frame(ev, "{}"));
    }

    [Fact]
    public void Frame_EmptyData_StillEmitsASingleDataLine()
    {
        Assert.Equal("event: done\ndata: \n\n", Sse.Frame("done", ""));
    }

    private static IEnumerable<string> SplitFrames(string stream)
    {
        int idx;
        while ((idx = stream.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
        {
            yield return stream[..idx];
            stream = stream[(idx + 2)..];
        }
    }
}
