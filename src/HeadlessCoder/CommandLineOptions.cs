namespace HeadlessCoder;

/// <summary>
/// Parsed command line options for HeadlessCoder.
/// </summary>
public sealed class CommandLineOptions
{
    /// <summary>TCP port the web UI listens on.</summary>
    public int Port { get; init; } = 8787;

    /// <summary>When true, keep the host machine awake while the app runs.</summary>
    public bool NoSleep { get; init; }

    /// <summary>Host/IP to bind Kestrel to. Defaults to all interfaces.</summary>
    public string BindAddress { get; init; } = "0.0.0.0";

    /// <summary>Optional explicit LAN IP to advertise in the URL/QR (auto-detected otherwise).</summary>
    public string? AdvertiseHost { get; init; }

    public bool ShowHelp { get; init; }

    public static CommandLineOptions Parse(string[] args)
    {
        int port = 8787;
        bool noSleep = false;
        bool help = false;
        string bind = "0.0.0.0";
        string? advertise = null;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].Trim();
            switch (a.ToLowerInvariant())
            {
                case "-ns":
                case "--no-sleep":
                    noSleep = true;
                    break;
                case "-h":
                case "-?":
                case "--help":
                    help = true;
                    break;
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int p))
                        port = p;
                    break;
                case "--bind":
                    if (i + 1 < args.Length)
                        bind = args[++i];
                    break;
                case "--host":
                    if (i + 1 < args.Length)
                        advertise = args[++i];
                    break;
                default:
                    // Allow "--port=8080" style too.
                    if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(a.AsSpan("--port=".Length), out int p2))
                        port = p2;
                    else if (a.StartsWith("--host=", StringComparison.OrdinalIgnoreCase))
                        advertise = a["--host=".Length..];
                    break;
            }
        }

        return new CommandLineOptions
        {
            Port = port,
            NoSleep = noSleep,
            ShowHelp = help,
            BindAddress = bind,
            AdvertiseHost = advertise,
        };
    }

    public static string HelpText =>
        """
        HeadlessCoder - manage your coding-agent CLIs (Claude Code, Gemini, Copilot) from your phone or any device on your LAN.

        Usage:
          headlesscoder [options]

        Options:
          -ns, --no-sleep     Prevent the host machine from going to sleep while running.
              --port <n>      Port to serve the web UI on (default: 8787).
              --bind <addr>   Address Kestrel binds to (default: 0.0.0.0 = all interfaces).
              --host <ip>     LAN IP to advertise in the printed URL/QR (auto-detected otherwise).
          -h, --help          Show this help.

        Once running, open the printed URL (or scan the QR code) on any device on the same network.
        """;
}
