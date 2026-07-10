using HeadlessCoder.Agents;

namespace HeadlessCoder.Tests;

public class CliTests : IDisposable
{
    private readonly string _dir;

    public CliTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hc-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Locate_FindsExecutableInExtraDir()
    {
        // The Windows probe list also includes the bare base name, so a plain file works on any OS.
        string baseName = "hctool" + Guid.NewGuid().ToString("N");
        string exe = Path.Combine(_dir, baseName);
        File.WriteAllText(exe, "#!/bin/sh\n");

        string? found = Cli.Locate(baseName, _dir);

        Assert.Equal(exe, found);
    }

    [Fact]
    public void Locate_ReturnsNull_WhenNotFound()
    {
        string baseName = "definitely-missing-" + Guid.NewGuid().ToString("N");
        Assert.Null(Cli.Locate(baseName, _dir));
    }

    [Fact]
    public void Locate_IgnoresBlankExtraDirs()
    {
        string baseName = "hctool" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(_dir, baseName), "x");

        // A stray empty/whitespace dir must not throw and must not prevent the real hit.
        string? found = Cli.Locate(baseName, "", "   ", _dir);

        Assert.Equal(Path.Combine(_dir, baseName), found);
    }
}
