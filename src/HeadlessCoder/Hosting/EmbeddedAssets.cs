using System.Reflection;

namespace HeadlessCoder.Hosting;

/// <summary>
/// Serves the web UI files that are embedded into the assembly, so the whole app
/// ships as a single self-contained binary.
/// </summary>
public static class EmbeddedAssets
{
    private static readonly Assembly Asm = typeof(EmbeddedAssets).Assembly;

    public static (byte[] Bytes, string ContentType)? TryGet(string relativePath)
    {
        // "app.js" -> resource whose name ends with ".Web.app.js"
        string normalized = relativePath.Replace('/', '.').TrimStart('.');
        string suffix = ".Web." + normalized;

        string? name = Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                                 || n.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return null;

        using var stream = Asm.GetManifestResourceStream(name);
        if (stream is null)
            return null;

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return (ms.ToArray(), ContentTypeFor(relativePath));
    }

    private static string ContentTypeFor(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".json" => "application/json; charset=utf-8",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream",
        };
    }
}
