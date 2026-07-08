using System.Text;
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
    private const string Bold = "[1m";

    static ConsoleUi()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* ignored */ }
    }

    public static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine($"{Coral}{Bold}  ✳  HeadlessCoder{Reset}{Muted}  ·  manage Claude Code from anywhere on your LAN{Reset}");
        Console.WriteLine();
    }

    public static void PrintStartup(string url, bool noSleep, string sleepStatus, string bind, int port)
    {
        Console.WriteLine($"{Muted}  Serving on {bind}:{port} (all interfaces){Reset}");
        Console.WriteLine();
        Console.WriteLine($"{Cream}  Open this on your phone or any device on the same network:{Reset}");
        Console.WriteLine($"{Coral}{Bold}      {url}{Reset}");
        Console.WriteLine();

        PrintQr(url);

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
