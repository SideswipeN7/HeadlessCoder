using HeadlessCoder.Terminal;

namespace HeadlessCoder.Tests;

public class CommandRunnerTests
{
    private static async Task<List<TerminalLine>> RunAsync(string command, string? cwd = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var lines = new List<TerminalLine>();
        await foreach (var line in new CommandRunner().RunAsync(command, cwd, cts.Token))
            lines.Add(line);
        return lines;
    }

    [Fact]
    public async Task RunAsync_StreamsStdout_AndTerminatesWithExitZero()
    {
        var lines = await RunAsync("echo hello");

        Assert.Contains(lines, l => l.Kind == "stdout" && l.Text.Contains("hello"));

        var last = lines[^1];
        Assert.Equal("exit", last.Kind);
        Assert.Equal("0", last.Text);
    }

    [Fact]
    public async Task RunAsync_ReportsNonZeroExitCode()
    {
        var lines = await RunAsync("exit 3");

        var last = lines[^1];
        Assert.Equal("exit", last.Kind);
        Assert.Equal("3", last.Text);
    }

    [Fact]
    public async Task RunAsync_FallsBackToHomeDirectory_ForInvalidCwd()
    {
        // A non-existent cwd must not crash; the command still runs from the home dir.
        var lines = await RunAsync("echo ok", cwd: Path.Combine("Z:", "no", "such", "path-" + Guid.NewGuid()));

        Assert.Contains(lines, l => l.Kind == "stdout" && l.Text.Contains("ok"));
        Assert.Equal("exit", lines[^1].Kind);
    }

    [Fact]
    public async Task RunAsync_CanBeCancelled_WithoutHanging()
    {
        using var cts = new CancellationTokenSource();
        var runner = new CommandRunner();

        string longCmd = OperatingSystem.IsWindows()
            ? "echo start && ping -n 20 127.0.0.1"
            : "echo start && sleep 20";

        var task = Task.Run(async () =>
        {
            var collected = new List<TerminalLine>();
            try
            {
                // A long-running command; we cancel almost immediately.
                await foreach (var line in runner.RunAsync(longCmd, null, cts.Token))
                {
                    collected.Add(line);
                    cts.Cancel();
                }
            }
            catch (OperationCanceledException) { /* expected */ }
            return collected;
        });

        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(task, completed); // it finished (did not hang) within the timeout
    }
}
