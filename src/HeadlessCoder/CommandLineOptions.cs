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

    /// <summary>When true, serve without any password (open access on the LAN).</summary>
    public bool NoPass { get; init; }

    /// <summary>Explicit password to protect access with. When null and not <see cref="NoPass"/>, one is generated.</summary>
    public string? Password { get; init; }

    /// <summary>When true, new sessions may target any folder; otherwise only existing project directories.</summary>
    public bool FreeStyle { get; init; }

    /// <summary>When true, do not read past sessions/transcripts from disk.</summary>
    public bool NoHistory { get; init; }

    /// <summary>When true, expose an in-browser terminal for running arbitrary shell commands.</summary>
    public bool CommandsAllowed { get; init; }

    public bool ShowHelp { get; init; }

    public static CommandLineOptions Parse(string[] args)
    {
        int port = 8787;
        bool noSleep = false;
        bool help = false;
        bool noPass = false;
        bool freeStyle = false;
        bool noHistory = false;
        bool commandsAllowed = false;
        string bind = "0.0.0.0";
        string? advertise = null;
        string? password = null;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].Trim();
            switch (a.ToLowerInvariant())
            {
                case "-ns":
                case "--no-sleep":
                    noSleep = true;
                    break;
                case "-np":
                case "--no-pass":
                    noPass = true;
                    break;
                case "-fs":
                case "--free-style":
                    freeStyle = true;
                    break;
                case "--no-history":
                    noHistory = true;
                    break;
                case "-ca":
                case "--commands-allowed":
                    commandsAllowed = true;
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
                case "--pass":
                    if (i + 1 < args.Length)
                        password = args[++i];
                    break;
                default:
                    // Allow "--port=8080" / "--pass=secret" style too.
                    if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(a.AsSpan("--port=".Length), out int p2))
                        port = p2;
                    else if (a.StartsWith("--host=", StringComparison.OrdinalIgnoreCase))
                        advertise = a["--host=".Length..];
                    else if (a.StartsWith("--pass=", StringComparison.OrdinalIgnoreCase))
                        password = a["--pass=".Length..];
                    else if (a.StartsWith("--bind=", StringComparison.OrdinalIgnoreCase))
                        bind = a["--bind=".Length..];
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
            NoPass = noPass,
            Password = password,
            FreeStyle = freeStyle,
            NoHistory = noHistory,
            CommandsAllowed = commandsAllowed,
        };
    }

    public static string HelpText =>
        """
        HeadlessCoder - manage your coding-agent CLIs (Claude Code, Antigravity, Copilot) from
        your phone or any device on your LAN.

        Usage:
          headlesscoder [options]

        Options:
          -ns, --no-sleep     Prevent the host machine from going to sleep while running.
          -np, --no-pass      Serve without a password (open access on your LAN).
              --pass <value>  Protect access with your own password.
          -fs, --free-style   Allow new sessions in ANY folder (not just existing projects).
              --no-history    Do not read past sessions/transcripts from disk.
          -ca, --commands-allowed
                              Show an in-browser terminal to run shell commands (use with care).
              --port <n>      Port to serve the web UI on (default: 8787).
              --bind <addr>   Address Kestrel binds to (default: 0.0.0.0 = all interfaces).
              --host <ip>     LAN IP to advertise in the printed URL/QR (auto-detected otherwise).
          -h,  --help         Show this help.

        Access control:
          By default a password is generated from Transformers names and printed at startup.
          The QR code embeds it, so scanning signs you in automatically; when you open the
          URL by hand you enter the password on a login screen.
          Use --pass to set your own, or --no-pass to disable it entirely.

        Once running, open the printed URL (or scan the QR code) on any device on the same
        network.
        """;
}
