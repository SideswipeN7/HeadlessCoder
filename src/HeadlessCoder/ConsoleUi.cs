using System.Text;
using HeadlessCoder.Agents;
using QRCoder;

namespace HeadlessCoder;

/// <summary>
/// Renders the startup banner, LAN URL and a scannable QR code straight to the terminal.
/// </summary>
public static class ConsoleUi
{
    // Anthropic-ish palette via 24-bit ANSI where supported.
    private const string Reset = "[0m";
    private const string Coral = "[38;2;204;120;92m";   // #cc785c
    private const string Cream = "[38;2;250;249;245m";  // #faf9f5
    private const string Muted = "[38;2;140;138;130m";  // #8c8a82
    private const string Green = "\x1b[38;2;93;184;114m";   // #5db872
    private const string Amber = "\x1b[38;2;232;165;90m";   // #e8a55a
    private const string Bold = "[1m";

    static ConsoleUi()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* ignored */ }
    }

    public static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine($"{Coral}{Bold}  ✳  HeadlessCoder{Reset}{Muted}  ·  manage your coding agents from anywhere on your LAN{Reset}");
        Console.WriteLine();
    }

    public static void PrintPortInUse(int port)
    {
        Console.WriteLine($"  {Amber}{Bold}⚠ Port {port} is already in use.{Reset}");
        Console.WriteLine($"{Muted}  Please start HeadlessCoder on another port, e.g.:{Reset}");
        Console.WriteLine($"{Coral}      headlesscoder --port {port + 1}{Reset}");
        Console.WriteLine();
    }

    public static void PrintStartup(string url, string qrContent, string? password,
        bool noSleep, string sleepStatus, string bind, int port)
    {
        Console.WriteLine($"{Muted}  Serving on {bind}:{port} (all interfaces){Reset}");
        Console.WriteLine();
        Console.WriteLine($"{Cream}  Open this on your phone or any device on the same network:{Reset}");
        Console.WriteLine($"{Coral}{Bold}      {url}{Reset}");
        Console.WriteLine();

        // The QR encodes the access key too, so scanning signs you in automatically.
        PrintQr(qrContent);

        Console.WriteLine();
        if (password is not null)
        {
            Console.WriteLine($"{Cream}  🔒 Access password{Reset} {Muted}(enter it if you open the link by hand):{Reset}");
            Console.WriteLine($"{Coral}{Bold}      {password}{Reset}");
        }
        else
        {
            Console.WriteLine($"{Muted}  🔓 Open access — no password (started with --no-pass).{Reset}");
        }
        Console.WriteLine();
        if (noSleep)
            Console.WriteLine($"{Muted}  ☕ Keep-awake active: {sleepStatus}{Reset}");
        else
            Console.WriteLine($"{Muted}  💤 Sleep allowed (start with --no-sleep / -ns to keep the machine awake){Reset}");
        Console.WriteLine($"{Muted}  Press Ctrl+C to stop.{Reset}");
        Console.WriteLine();
    }

    /// <summary>
    /// Renders a QR code using Unicode half-blocks (two module rows per text row).
    /// Light modules render as blocks and dark modules as spaces, which scans reliably
    /// on the dark terminals most people use.
    /// </summary>
    /// <summary>
    /// Prints the startup preflight: which agent CLIs were detected, and what the
    /// user must do to enable the ones that are missing or half-configured.
    /// </summary>
    public static void PrintDoctor(DoctorReport report)
    {
        Console.WriteLine($"{Cream}  Preflight — agent CLIs on this machine:{Reset}");
        foreach (var a in report.Agents)
        {
            (string glyph, string color) = a.Status switch
            {
                "ready" => ("✓", Green),
                "partial" => ("●", Amber),
                _ => ("○", Muted),
            };

            var detail = new StringBuilder();
            if (a.Installed)
            {
                detail.Append(a.Version is { Length: > 0 } v ? v : "installed");
                if (a.SupportsHistory && a.SessionCount > 0)
                    detail.Append($"  ·  {a.SessionCount} sessions");
            }
            else
            {
                detail.Append("not installed");
            }

            Console.WriteLine($"    {color}{glyph}{Reset} {Cream}{a.DisplayName}{Reset} {Muted}— {detail}{Reset}");
            if (!string.IsNullOrWhiteSpace(a.Remediation))
                Console.WriteLine($"        {Muted}↳ {a.Remediation}{Reset}");
        }

        if (!report.AnyAgentAvailable)
        {
            Console.WriteLine();
            Console.WriteLine($"  {Amber}{Bold}⚠ No agent CLI detected.{Reset} {Muted}Install at least one (see hints above) before use.{Reset}");
        }
        Console.WriteLine();
    }

    public static void PrintQr(string content)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        int size = matrix.Count;

        const int quiet = 2;           // quiet-zone border in modules
        int full = size + quiet * 2;
        const string pad = "  ";       // left indent

        // Returns true when the module is "light" (renders as a block).
        bool Light(int row, int col)
        {
            if (row < quiet || col < quiet || row >= size + quiet || col >= size + quiet)
                return true; // quiet zone is light
            return !matrix[row - quiet][col - quiet];
        }

        var sb = new StringBuilder();
        for (int row = 0; row < full; row += 2)
        {
            sb.Append(pad).Append(Cream);
            for (int col = 0; col < full; col++)
            {
                bool top = Light(row, col);
                bool bottom = row + 1 < full && Light(row + 1, col);
                sb.Append((top, bottom) switch
                {
                    (true, true) => '█',
                    (true, false) => '▀',
                    (false, true) => '▄',
                    _ => ' ',
                });
            }
            sb.Append(Reset).Append('\n');
        }
        Console.Write(sb.ToString());
    }
}
