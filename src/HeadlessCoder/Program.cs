using System.Net;
using System.Reflection;
using System.Text.Json;
using HeadlessCoder;
using HeadlessCoder.Agents;
using HeadlessCoder.Auth;
using HeadlessCoder.Claude;
using HeadlessCoder.Hosting;
using HeadlessCoder.Networking;
using HeadlessCoder.Platform;
using HeadlessCoder.Terminal;
using Microsoft.AspNetCore.Http.Features;
using QRCoder;

var options = CommandLineOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(CommandLineOptions.HelpText);
    return;
}

if (!options.NoLogo)
    ConsoleUi.PrintLogo();
ConsoleUi.PrintBanner();

// Fail fast (with a friendly message) if the port is already taken.
if (!NetworkHelper.IsPortAvailable(options.BindAddress, options.Port))
{
    ConsoleUi.PrintPortInUse(options.Port);
    Environment.Exit(1);
}

// Resolve access control: --no-pass disables it, --pass sets an explicit password,
// otherwise generate a memorable one from Transformers names.
bool authEnabled = !options.NoPass;
string password = authEnabled
    ? (!string.IsNullOrWhiteSpace(options.Password) ? options.Password! : TransformersPassword.Generate())
    : "";
string authToken = authEnabled ? Guid.NewGuid().ToString("N") : "";

// LAN URL + QR content are needed by the startup banner and the /api/qr + /api/config
// endpoints, so resolve them once up front. The QR embeds the access key so a scan signs in.
string host = options.AdvertiseHost ?? NetworkHelper.GetLanIpv4();
string url = $"http://{host}:{options.Port}";
string qrContent = authEnabled ? $"{url}/?key={Uri.EscapeDataString(password)}" : url;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
// Logging is suppressed by default so the console stays clean; --logs re-enables it.
if (!options.Logs)
    builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");

// Agent providers + registry.
builder.Services.AddSingleton<ClaudeSessionStore>(_ => new ClaudeSessionStore());
builder.Services.AddSingleton<ClaudeCliRunner>(_ => new ClaudeCliRunner());
builder.Services.AddSingleton<IAgentProvider>(sp =>
    new ClaudeProvider(sp.GetRequiredService<ClaudeSessionStore>(), sp.GetRequiredService<ClaudeCliRunner>()));
builder.Services.AddSingleton<IAgentProvider, AntigravityProvider>();
builder.Services.AddSingleton<IAgentProvider, CopilotProvider>();
builder.Services.AddSingleton<IAgentProvider, CodexProvider>();
builder.Services.AddSingleton<IAgentProvider, OpencodeProvider>();
builder.Services.AddSingleton<IAgentProvider, CursorProvider>();
builder.Services.AddSingleton<IAgentProvider, AiderProvider>();
builder.Services.AddSingleton<IAgentProvider, QwenProvider>();
builder.Services.AddSingleton<IAgentProvider, DeepSeekProvider>();
builder.Services.AddSingleton<AgentRegistry>(sp =>
    new AgentRegistry(sp.GetServices<IAgentProvider>()));
builder.Services.AddSingleton<SessionTitleStore>(_ => new SessionTitleStore());
builder.Services.AddSingleton<CommandRunner>();

var app = builder.Build();
var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// About / update-check.
const string ghRepo = "SideswipeN7/HeadlessCoder";
string appVersion = Assembly.GetExecutingAssembly().GetName().Version is { } ver
    ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "0.0.6";
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

// ---- Access control ----------------------------------------------------------
// A valid `?key=<password>` (as embedded in the QR) or the login form sets a
// session cookie; everything else is gated behind it.
if (authEnabled)
{
    app.Use(async (ctx, next) =>
    {
        string path = ctx.Request.Path.Value ?? "";
        bool openPath =
            path.Equals("/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/logo.svg", StringComparison.OrdinalIgnoreCase) ||
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
        // Preserve the target (e.g. a shared ?s= deep link) across login.
        string ret = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
        ctx.Response.Redirect($"/login?return={ret}");
    });
}

app.MapGet("/login", () => authEnabled ? ServeAsset("login.html") : Results.Redirect("/"));
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

// ---- Web UI (embedded, single-file) — served at the root ---------------------

app.MapGet("/", () => ServeAsset("index.html"));
app.MapGet("/{file}", (string file) =>
{
    var asset = EmbeddedAssets.TryGet(file);
    return asset is null ? ServeAsset("index.html") : Results.Bytes(asset.Value.Bytes, asset.Value.ContentType);
});

// ---- REST API ----------------------------------------------------------------

app.MapGet("/api/health", (AgentRegistry reg) => Results.Json(new
{
    ok = true,
    anyAgent = reg.Diagnose().AnyAgentAvailable,
    auth = authEnabled,
    freeStyle = options.FreeStyle,
    noHistory = options.NoHistory,
    commandsAllowed = options.CommandsAllowed,
}, jsonOpts));

// The effective runtime configuration, for the Settings → Config panel.
app.MapGet("/api/config", () => Results.Json(new
{
    url,
    port = options.Port,
    bind = options.BindAddress,
    host,
    auth = authEnabled,
    noSleep = options.NoSleep,
    freeStyle = options.FreeStyle,
    noHistory = options.NoHistory,
    commandsAllowed = options.CommandsAllowed,
}, jsonOpts));

// The access QR as a PNG (same content as the console QR: URL + embedded key).
app.MapGet("/api/qr", () =>
{
    using var gen = new QRCodeGenerator();
    using var data = gen.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.M);
    byte[] png = new PngByteQRCode(data).GetGraphic(10);
    return Results.Bytes(png, "image/png");
});

// About: version + repo.
app.MapGet("/api/about", () => Results.Json(new
{
    name = "HeadlessCoder",
    version = appVersion,
    repo = ghRepo,
    repoUrl = $"https://github.com/{ghRepo}",
    runtime = Environment.Version.ToString(),
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
}, jsonOpts));

// Check GitHub Releases for a newer version.
app.MapGet("/api/update-check", async (HttpContext ctx) =>
{
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{ghRepo}/releases/latest");
        req.Headers.UserAgent.ParseAdd("HeadlessCoder-update-check");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var resp = await http.SendAsync(req, ctx.RequestAborted);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Results.Json(new { current = appVersion, latest = (string?)null, updateAvailable = false,
                url = (string?)null, note = "No releases published yet." }, jsonOpts);

        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ctx.RequestAborted));
        var root = doc.RootElement;
        string? tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        string? url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
        bool available = IsNewer(appVersion, tag);
        return Results.Json(new { current = appVersion, latest = tag, updateAvailable = available, url }, jsonOpts);
    }
    catch (Exception ex)
    {
        return Results.Json(new { current = appVersion, error = "Could not reach GitHub: " + ex.Message }, jsonOpts);
    }
});

// Preflight / doctor: what's installed and what to do about what isn't.
app.MapGet("/api/agents", (AgentRegistry reg) => Results.Json(reg.Diagnose().Agents, jsonOpts));

// Distinct working directories for the new-session picker.
app.MapGet("/api/projects", (AgentRegistry reg) =>
    Results.Json(options.NoHistory
        ? Enumerable.Empty<object>()
        : reg.ListWorkingDirectories().Select(p => new { path = p }), jsonOpts));

// All sessions across every agent (optionally filtered to one provider).
app.MapGet("/api/sessions", (AgentRegistry reg, SessionTitleStore titles, string? provider) =>
{
    if (options.NoHistory)
        return Results.Json(Array.Empty<object>(), jsonOpts);
    var all = reg.ListAllSessions();
    if (!string.IsNullOrWhiteSpace(provider))
        all = all.Where(s => string.Equals(s.Provider, provider, StringComparison.OrdinalIgnoreCase)).ToList();

    // Apply user-chosen titles.
    var overrides = titles.Snapshot();
    var withTitles = all.Select(s =>
        overrides.TryGetValue(s.Id, out var t) ? s with { Title = t } : s).ToList();
    return Results.Json(withTitles, jsonOpts);
});

// Rename a session (blank title clears the override).
app.MapPost("/api/sessions/{id}/rename", async (HttpContext ctx, SessionTitleStore titles, string id) =>
{
    var body = await JsonSerializer.DeserializeAsync<RenameBody>(ctx.Request.Body, jsonOpts, ctx.RequestAborted);
    titles.Set(id, body?.Title);
    return Results.Json(new { ok = true, title = titles.Get(id) }, jsonOpts);
});

app.MapGet("/api/sessions/{provider}/{project}/{id}", (AgentRegistry reg, string provider, string project, string id) =>
{
    if (options.NoHistory)
        return Results.Json(Array.Empty<object>(), jsonOpts);
    var p = reg.Get(provider);
    return p is null
        ? Results.NotFound($"Unknown agent '{provider}'.")
        : Results.Json(p.GetTranscript(project, id), jsonOpts);
});

// Last-known usage/context for a session, so the composer shows it the moment you open a chat.
app.MapGet("/api/sessions/{provider}/{project}/{id}/usage", (AgentRegistry reg, string provider, string project, string id) =>
{
    if (options.NoHistory) return Results.Json((object?)null, jsonOpts);
    var p = reg.Get(provider);
    return Results.Json(p?.GetLastUsage(project, id), jsonOpts);
});

// Delete a session's stored transcript (used to keep in-private sessions out of history).
app.MapPost("/api/sessions/{provider}/{id}/purge", (AgentRegistry reg, string provider, string id) =>
{
    var p = reg.Get(provider);
    bool removed = p is not null && p.PurgeSession(id);
    return Results.Json(new { removed }, jsonOpts);
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

    // Without --free-style, new sessions are restricted to existing project folders.
    if (isNew && !options.FreeStyle)
    {
        var known = reg.ListWorkingDirectories();
        bool allowed = known.Any(d => string.Equals(d, req.Cwd, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("folder not allowed (start with --free-style to use any folder)");
            return;
        }
    }

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

// Save a pasted/uploaded image to a temp file and return its absolute path so the
// message can reference it and the agent can read it from disk.
app.MapPost("/api/upload", async (HttpContext ctx) =>
{
    string name, dataBase64;
    using (var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted))
    {
        name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        dataBase64 = doc.RootElement.TryGetProperty("dataBase64", out var d) ? d.GetString() ?? "" : "";
    }
    if (string.IsNullOrEmpty(dataBase64))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("dataBase64 is required");
        return;
    }
    byte[] bytes;
    try { bytes = Convert.FromBase64String(dataBase64); }
    catch { ctx.Response.StatusCode = StatusCodes.Status400BadRequest; await ctx.Response.WriteAsync("invalid base64"); return; }
    if (bytes.Length > 25 * 1024 * 1024)
    {
        ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await ctx.Response.WriteAsync("image too large (max 25 MB)");
        return;
    }

    string ext = Path.GetExtension(name);
    if (string.IsNullOrWhiteSpace(ext) || ext.Length > 6) ext = ".png";
    string dir = Path.Combine(Path.GetTempPath(), "headlesscoder-uploads");
    Directory.CreateDirectory(dir);
    string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
    await File.WriteAllBytesAsync(path, bytes, ctx.RequestAborted);
    await ctx.Response.WriteAsJsonAsync(new { path }, jsonOpts);
});

// In-browser terminal — only mounted when the server is started with --commands-allowed.
// Streams a single shell command's stdout/stderr line-by-line over SSE.
app.MapPost("/api/terminal", async (HttpContext ctx, CommandRunner runner) =>
{
    if (!options.CommandsAllowed)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsync("terminal is disabled (start with --commands-allowed)");
        return;
    }

    string command, cwd;
    using (var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted))
    {
        command = doc.RootElement.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
        cwd = doc.RootElement.TryGetProperty("cwd", out var w) ? w.GetString() ?? "" : "";
    }
    if (string.IsNullOrWhiteSpace(command))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("command is required");
        return;
    }

    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";
    ctx.Response.ContentType = "text/event-stream";
    ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

    try
    {
        await foreach (var line in runner.RunAsync(command, cwd, ctx.RequestAborted))
            await WriteSse(ctx, "line",
                JsonSerializer.Serialize(new { kind = line.Kind, text = line.Text }, jsonOpts));
        await WriteSse(ctx, "done", "{}");
    }
    catch (OperationCanceledException)
    {
        // client disconnected
    }
    catch (Exception ex)
    {
        await WriteSse(ctx, "line",
            JsonSerializer.Serialize(new { kind = "stderr", text = ex.Message }, jsonOpts));
    }
});

// ---- Startup: doctor, URL, QR, keep-awake ------------------------------------

var registry = app.Services.GetRequiredService<AgentRegistry>();
DoctorReport doctor = registry.Diagnose();

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

static Task WriteSse(HttpContext ctx, string @event, string data) =>
    Sse.WriteAsync(ctx, @event, data);

// True when the release tag (e.g. "v1.2.0") is a higher version than the running app.
static bool IsNewer(string current, string? tag) => VersionCheck.IsNewer(current, tag);

static bool FixedEquals(string a, string b) => SecurityUtil.FixedEquals(a, b);

static void AppendAuthCookie(HttpContext ctx, string token) =>
    ctx.Response.Cookies.Append("hc_auth", token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = TimeSpan.FromDays(30),
    });

sealed record LoginBody(string? Password);
sealed record RenameBody(string? Title);
