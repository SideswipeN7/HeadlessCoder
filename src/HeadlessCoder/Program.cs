using System.Text;
using System.Text.Json;
using HeadlessCoder;
using HeadlessCoder.Claude;
using HeadlessCoder.Hosting;
using HeadlessCoder.Networking;
using HeadlessCoder.Platform;
using Microsoft.AspNetCore.Http.Features;

var options = CommandLineOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(CommandLineOptions.HelpText);
    return;
}

ConsoleUi.PrintBanner();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Keep framework logging quiet so the banner/QR stay readable.
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");

builder.Services.AddSingleton<ClaudeSessionStore>(_ => new ClaudeSessionStore());
builder.Services.AddSingleton<ClaudeCliRunner>(_ => new ClaudeCliRunner());

var app = builder.Build();

var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// ---- Web UI (embedded, single-file) ------------------------------------------

app.MapGet("/", () => Results.Redirect("/hc"));

app.MapGet("/hc", () => ServeAsset("index.html"));
app.MapGet("/hc/{*path}", (string path) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return ServeAsset("index.html");
    var asset = EmbeddedAssets.TryGet(path);
    return asset is null ? ServeAsset("index.html") : Results.Bytes(asset.Value.Bytes, asset.Value.ContentType);
});

// ---- REST API ----------------------------------------------------------------

app.MapGet("/api/health", (ClaudeSessionStore store, ClaudeCliRunner runner) => Results.Json(new
{
    ok = true,
    projectsRoot = store.ProjectsRoot,
    storeAvailable = store.IsAvailable,
    claude = runner.Executable,
}, jsonOpts));

app.MapGet("/api/projects", (ClaudeSessionStore store) =>
    Results.Json(store.ListProjects(), jsonOpts));

app.MapGet("/api/sessions", (ClaudeSessionStore store, string? project) =>
    Results.Json(store.ListSessions(project), jsonOpts));

app.MapGet("/api/sessions/{project}/{id}", (ClaudeSessionStore store, string project, string id) =>
    Results.Json(store.GetTranscript(project, id), jsonOpts));

// Stream a message to a (possibly new) session as Server-Sent Events.
app.MapPost("/api/message", async (HttpContext ctx, ClaudeCliRunner runner) =>
{
    var req = await JsonSerializer.DeserializeAsync<SendMessageRequest>(
        ctx.Request.Body, jsonOpts, ctx.RequestAborted);
    if (req is null || string.IsNullOrWhiteSpace(req.Message))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("message is required");
        return;
    }

    // A brand-new session needs an id we can hand back to the client immediately.
    bool isNew = string.IsNullOrWhiteSpace(req.SessionId);
    req.IsNewSession = isNew;
    if (isNew)
        req.SessionId = Guid.NewGuid().ToString();

    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";
    ctx.Response.ContentType = "text/event-stream";
    ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

    await WriteSse(ctx, "meta", JsonSerializer.Serialize(
        new { sessionId = req.SessionId, isNew }, jsonOpts));

    try
    {
        await foreach (var line in runner.SendAsync(req, ctx.RequestAborted))
            await WriteSse(ctx, "event", line);
        await WriteSse(ctx, "done", "{}");
    }
    catch (OperationCanceledException)
    {
        // client disconnected — nothing to do
    }
    catch (Exception ex)
    {
        await WriteSse(ctx, "error", JsonSerializer.Serialize(new { error = ex.Message }, jsonOpts));
    }
});

// ---- Startup: URL, QR, keep-awake --------------------------------------------

string host = options.AdvertiseHost ?? NetworkHelper.GetLanIpv4();
string url = $"http://{host}:{options.Port}/hc";

SleepPreventer? sleep = options.NoSleep ? SleepPreventer.Start() : null;
app.Lifetime.ApplicationStopping.Register(() => sleep?.Dispose());

app.Lifetime.ApplicationStarted.Register(() =>
    ConsoleUi.PrintStartup(url, options.NoSleep, sleep?.Status ?? "n/a", options.BindAddress, options.Port));

await app.RunAsync();
sleep?.Dispose();
return;

// ---- local helpers -----------------------------------------------------------

static IResult ServeAsset(string name)
{
    var asset = EmbeddedAssets.TryGet(name);
    return asset is null
        ? Results.NotFound($"Embedded asset '{name}' not found.")
        : Results.Bytes(asset.Value.Bytes, asset.Value.ContentType);
}

static async Task WriteSse(HttpContext ctx, string @event, string data)
{
    var sb = new StringBuilder();
    sb.Append("event: ").Append(@event).Append('\n');
    // Data may be multi-line; SSE requires each line prefixed with "data: ".
    foreach (var l in data.Split('\n'))
        sb.Append("data: ").Append(l).Append('\n');
    sb.Append('\n');
    await ctx.Response.WriteAsync(sb.ToString(), ctx.RequestAborted);
    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
}
