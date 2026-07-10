namespace HeadlessCoder.Tests;

public class CommandLineOptionsTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsDefaults()
    {
        var o = CommandLineOptions.Parse(Array.Empty<string>());

        Assert.Equal(8787, o.Port);
        Assert.Equal("0.0.0.0", o.BindAddress);
        Assert.Null(o.AdvertiseHost);
        Assert.Null(o.Password);
        Assert.False(o.NoSleep);
        Assert.False(o.NoPass);
        Assert.False(o.FreeStyle);
        Assert.False(o.NoHistory);
        Assert.False(o.CommandsAllowed);
        Assert.False(o.NoLogo);
        Assert.False(o.Logs);
        Assert.False(o.ShowHelp);
    }

    [Theory]
    [InlineData("-ns")]
    [InlineData("--no-sleep")]
    public void Parse_NoSleepFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).NoSleep);

    [Theory]
    [InlineData("-np")]
    [InlineData("--no-pass")]
    public void Parse_NoPassFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).NoPass);

    [Theory]
    [InlineData("-fs")]
    [InlineData("--free-style")]
    public void Parse_FreeStyleFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).FreeStyle);

    [Theory]
    [InlineData("-ca")]
    [InlineData("--commands-allowed")]
    public void Parse_CommandsAllowedFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).CommandsAllowed);

    [Theory]
    [InlineData("-nl")]
    [InlineData("--no-logo")]
    public void Parse_NoLogoFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).NoLogo);

    [Theory]
    [InlineData("-l")]
    [InlineData("--logs")]
    public void Parse_LogsFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).Logs);

    [Fact]
    public void Parse_NoHistoryFlag() => Assert.True(CommandLineOptions.Parse(["--no-history"]).NoHistory);

    [Theory]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("--help")]
    public void Parse_HelpFlag(string flag) => Assert.True(CommandLineOptions.Parse([flag]).ShowHelp);

    [Theory]
    [InlineData(new[] { "-p", "9000" }, 9000)]
    [InlineData(new[] { "--port", "1234" }, 1234)]
    [InlineData(new[] { "--port=4321" }, 4321)]
    [InlineData(new[] { "-p=5678" }, 5678)]
    public void Parse_Port(string[] args, int expected)
    {
        Assert.Equal(expected, CommandLineOptions.Parse(args).Port);
    }

    [Theory]
    [InlineData("-p", "abc")]      // non-numeric consumes the value but keeps default
    [InlineData("--port=xyz")]     // non-numeric inline form keeps default
    public void Parse_InvalidPort_KeepsDefault(params string[] args)
    {
        Assert.Equal(8787, CommandLineOptions.Parse(args).Port);
    }

    [Theory]
    [InlineData(new[] { "--pass", "s3cr3t" }, "s3cr3t")]
    [InlineData(new[] { "--pass=hunter2" }, "hunter2")]
    public void Parse_Password(string[] args, string expected)
    {
        Assert.Equal(expected, CommandLineOptions.Parse(args).Password);
    }

    [Theory]
    [InlineData(new[] { "--bind", "127.0.0.1" }, "127.0.0.1")]
    [InlineData(new[] { "--bind=192.168.0.5" }, "192.168.0.5")]
    public void Parse_Bind(string[] args, string expected)
    {
        Assert.Equal(expected, CommandLineOptions.Parse(args).BindAddress);
    }

    [Theory]
    [InlineData(new[] { "--host", "10.0.0.1" }, "10.0.0.1")]
    [InlineData(new[] { "--host=10.0.0.9" }, "10.0.0.9")]
    public void Parse_AdvertiseHost(string[] args, string expected)
    {
        Assert.Equal(expected, CommandLineOptions.Parse(args).AdvertiseHost);
    }

    [Fact]
    public void Parse_IsCaseInsensitiveForFlags()
    {
        var o = CommandLineOptions.Parse(["--NO-PASS", "--Free-Style"]);
        Assert.True(o.NoPass);
        Assert.True(o.FreeStyle);
    }

    [Fact]
    public void Parse_CombinesMultipleOptions()
    {
        var o = CommandLineOptions.Parse(
            ["--port", "9999", "--no-pass", "-ca", "--host", "10.1.2.3", "--free-style"]);

        Assert.Equal(9999, o.Port);
        Assert.True(o.NoPass);
        Assert.True(o.CommandsAllowed);
        Assert.True(o.FreeStyle);
        Assert.Equal("10.1.2.3", o.AdvertiseHost);
    }

    [Fact]
    public void Parse_TrailingValuelessPortFlag_IsIgnored()
    {
        // "--port" with no following token must not throw and keeps the default.
        Assert.Equal(8787, CommandLineOptions.Parse(["--port"]).Port);
    }

    [Fact]
    public void HelpText_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(CommandLineOptions.HelpText));
        Assert.Contains("Usage:", CommandLineOptions.HelpText);
    }
}
