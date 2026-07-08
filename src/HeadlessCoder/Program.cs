using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessCoder;
using HeadlessCoder.Agents;
using HeadlessCoder.Auth;
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

// Resolve access control: --no-pass disables it, --pass sets an explicit password,
// otherwise generate a memorable one from Transformers names.
bool authEnabled = !options.NoPass;
string password = authEnabled
    ? (!string.IsNullOrWhiteSpace(options.Password) ? options.Password! : TransformersPassword.Generate())
    : "";
string authToken = authEnabled ? Guid.NewGuid().ToString("N") : "";

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");

// Agent providers + registry.
builder.Services.AddSingleton<ClaudeSessionStore>(_ => new ClaudeSessionStore());
builder.Services.AddSingleton<ClaudeCliRunner>(_ => new ClaudeCliRunner());
builder.Services.AddSingleton<IAgentProvider>(sp =>
    new ClaudeProvider(sp.GetRequiredService<ClaudeSessionStore>(), sp.GetRequiredService<ClaudeCliRunner>()));
builder.Services.AddSingleton<IAgentProvider, GeminiProvider>();
builder.Services.AddSingleton<IAgentProvider, CopilotProvider>();
builder.Services.AddSingleton<AgentRegistry>(sp =>
    new AgentRegistry(sp.GetServices<IAgentProvider>()));

var app = builder.Build();
var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// ---- Access control ----------------------------------------------------------
// A valid `?key=<password>` (as embedded in the QR) or the login form sets a
// session cookie; everything else is gated behind it.
if (authEnabled)
{
    app.Use(async (ctx, next) =>
    {
        string path = ctx.Request.Path.Value ?? "";
        bool openPath =
            path.Equals("/hc/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
        if (openPath) { await next(); return; }

        string? key = ctx.Request.Query["key"];
        if (key is not null && FixedEquals(key, password))
        {
            AppendAuthCookie(ctx, authToken);
            await next();
            return;
        }
        if (ctx.Request.Cookies.TryGetValue("hc_auth", out var cookie) && FixedEquals(cookie, authToken))
        {
            await next();
            return;
        }

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }
        ctx.Response.Redirect("/hc/login");
    });
}

app.MapGet("/hc/login", () => authEnabled ? ServeAsset("login.html") : Results.Redirect("/hc"));
app.MapPost("/api/login", async (HttpContext ctx) =>
{
    if (!authEnabled) return Results.Ok(new { ok = true });
    var body = await JsonSerializer.DeserializeAsync<LoginBody>(ctx.Request.Body, jsonOpts, ctx.RequestAborted);
    if (body is not null && FixedEquals(body.Password ?? "", password))
    {
        AppendAuthCookie(ctx, authToken);
        return Results.Ok(new { ok = true });
    }
    return Results.Unauthorized();
});

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

app.MapGet("/api/health", (AgentRegistry reg) => Results.Json(new
{
    ok = true,
    anyAgent = reg.Diagnose().AnyAgentAvailable,
    auth = authEnabled,
}, jsonOpts));

// Preflight / doctor: what's installed and what to do about what isn't.
app.MapGet("/api/agents", (AgentRegistry reg) => Results.Json(reg.Diagnose().Agents, jsonOpts));

// Distinct working directories for the new-session picker.
app.MapGet("/api/projects", (AgentRegistry reg) =>
    Results.Json(reg.ListWorkingDirectories().Select(p => new { path = p }), jsonOpts));

// All sessions across every agent (optionally filtered to one provider).
app.MapGet("/api/sessions", (AgentRegistry reg, string? provider) =>
{
    var all = reg.ListAllSessions();
    if (!string.IsNullOrWhiteSpace(provider))
        all = all.Where(s => string.Equals(s.Provider, provider, StringComparison.OrdinalIgnoreCase)).ToList();
    return Results.Json(all, jsonOpts);
});

app.MapGet("/api/sessions/{provider}/{project}/{id}", (AgentRegistry reg, string provider, string project, string id) =>
{
    var p = reg.Get(provider);
    return p is null
        ? Results.NotFound($"Unknown agent '{provider}'.")
        : Results.Json(p.GetTranscript(project, id), jsonOpts);
});

// Stream a message to a (possibly new) session as Server-Sent Events.
app.MapPost("/api/message", async (HttpContext ctx, AgentRegistry reg) =>
{
    var req = await JsonSerializer.DeserializeAsync<SendMessageRequest>(
        ctx.Request.Body, jsonOpts, ctx.RequestAborted);
    if (req is null || string.IsNullOrWhiteSpace(req.Message))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("message is required");
        return;
    }

    var provider = reg.Get(req.Provider);
    if (provider is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync($"unknown agent '{req.Provider}'");
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
        new { sessionId = req.SessionId, isNew, provider = provider.Id }, jsonOpts));

    try
    {
        await foreach (var ev in provider.SendAsync(req, ctx.RequestAborted))
            await WriteSse(ctx, "agent", JsonSerializer.Serialize(ev, jsonOpts));
        await WriteSse(ctx, "done", "{}");
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
    catch (Exception ex)
    {
        await WriteSse(ctx, "agent",
            JsonSerializer.Serialize(AgentEvent.Error(ex.Message), jsonOpts));
    }
});

// ---- Startup: doctor, URL, QR, keep-awake ------------------------------------

var registry = app.Services.GetRequiredService<AgentRegistry>();
DoctorReport doctor = registry.Diagnose();

string host = options.AdvertiseHost ?? NetworkHelper.GetLanIpv4();
string url = $"http://{host}:{options.Port}/hc";
// The QR embeds the access key so scanning signs you in automatically.
string qrContent = authEnabled ? $"{url}?key={Uri.EscapeDataString(password)}" : url;

SleepPreventer? sleep = options.NoSleep ? SleepPreventer.Start() : null;
app.Lifetime.ApplicationStopping.Register(() => sleep?.Dispose());

app.Lifetime.ApplicationStarted.Register(() =>
{
    ConsoleUi.PrintDoctor(doctor);
    ConsoleUi.PrintStartup(url, qrContent, authEnabled ? password : null,
        options.NoSleep, sleep?.Status ?? "n/a", options.BindAddress, options.Port);
});

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
    foreach (var l in data.Split('\n'))
        sb.Append("data: ").Append(l).Append('\n');
    sb.Append('\n');
    await ctx.Response.WriteAsync(sb.ToString(), ctx.RequestAborted);
    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
}

static bool FixedEquals(string a, string b)
{
    var ba = Encoding.UTF8.GetBytes(a);
    var bb = Encoding.UTF8.GetBytes(b);
    return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
}

static void AppendAuthCookie(HttpContext ctx, string token) =>
    ctx.Response.Cookies.Append("hc_auth", token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromDays(30),
    });

sealed record LoginBody(string? Password);
