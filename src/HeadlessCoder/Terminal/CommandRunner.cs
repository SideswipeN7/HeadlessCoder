using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace HeadlessCoder.Terminal;

/// <summary>One line of terminal output. <see cref="Kind"/> is "stdout" | "stderr" | "exit".</summary>
public sealed record TerminalLine(string Kind, string Text);

/// <summary>
/// Runs a single shell command and streams its stdout/stderr line-by-line, in order.
/// Only reachable when the server is started with <c>--commands-allowed</c>; it is a
/// plain command runner (one command per call), not a persistent PTY.
/// </summary>
public sealed class CommandRunner
{
    public async IAsyncEnumerable<TerminalLine> RunAsync(
        string command, string? cwd, [EnumeratorCancellation] CancellationToken ct)
    {
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var psi = new ProcessStartInfo
        {
            FileName = windows ? "cmd.exe" : "/bin/bash",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = ResolveWorkingDirectory(cwd),
        };
        if (windows) { psi.ArgumentList.Add("/c"); psi.ArgumentList.Add(command); }
        else { psi.ArgumentList.Add("-lc"); psi.ArgumentList.Add(command); }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Merge stdout + stderr into a single ordered channel.
        var channel = Channel.CreateUnbounded<TerminalLine>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });

        if (!process.Start())
            throw new InvalidOperationException("Failed to start the shell process.");

        process.StandardInput.Close();

        await using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* ignored */ }
        });

        // Pump each stream on its own task; complete the channel when both are drained.
        var pumps = Task.WhenAll(
            Pump(process.StandardOutput, "stdout", channel.Writer, ct),
            Pump(process.StandardError, "stderr", channel.Writer, ct));
        _ = pumps.ContinueWith(_ => channel.Writer.TryComplete(), TaskScheduler.Default);

        await foreach (var line in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            yield return line;

        try { await process.WaitForExitAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { yield break; }

        yield return new TerminalLine("exit", process.ExitCode.ToString());
    }

    private static async Task Pump(
        StreamReader reader, string kind, ChannelWriter<TerminalLine> writer, CancellationToken ct)
    {
        string? line;
        try
        {
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
                await writer.WriteAsync(new TerminalLine(kind, line), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* cancelled — stop pumping */ }
    }

    // Fall back to the user's home directory when no valid working directory is given.
    private static string ResolveWorkingDirectory(string? cwd)
    {
        if (!string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd)) return cwd!;
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
