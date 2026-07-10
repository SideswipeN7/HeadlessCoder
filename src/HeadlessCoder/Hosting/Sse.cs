using System.Text;
using Microsoft.AspNetCore.Http;

namespace HeadlessCoder.Hosting;

/// <summary>
/// Server-Sent Events framing. Kept as a standalone helper (rather than inline in
/// the endpoints) so the exact wire format can be unit-tested independently of a
/// running server.
/// </summary>
public static class Sse
{
    /// <summary>
    /// Builds a single SSE frame: an <c>event:</c> line, one <c>data:</c> line per
    /// line of <paramref name="data"/> (so multi-line JSON stays a valid single event),
    /// terminated by a blank line.
    /// </summary>
    public static string Frame(string @event, string data)
    {
        var sb = new StringBuilder();
        sb.Append("event: ").Append(@event).Append('\n');
        foreach (var l in data.Split('\n'))
            sb.Append("data: ").Append(l).Append('\n');
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Writes a single SSE frame to the response and flushes it immediately.</summary>
    public static async Task WriteAsync(HttpContext ctx, string @event, string data)
    {
        await ctx.Response.WriteAsync(Frame(@event, data), ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }
}
