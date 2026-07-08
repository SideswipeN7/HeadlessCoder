# ✳ HeadlessCoder

Manage multiple **Claude Code** sessions from your phone — or any device on your LAN.

HeadlessCoder runs on the machine where Claude Code lives, serves a small web UI, and
prints a **URL + QR code** in the console. Scan it from your phone and you get a warm,
Anthropic-styled interface to browse every Claude Code session on the machine, resume
any of them, and start new ones — with live streaming responses.

```
  ✳  HeadlessCoder  ·  manage Claude Code from anywhere on your LAN

  Open this on your phone or any device on the same network:
      http://192.168.100.1:8787/hc

  █████████████████████████████████████
  ██████ ▄▄▄▄▄ ██▄▄ ▄▄▄▀██ ▄▄▄▄▄ ██████
  ██████ █   █ █ ▀  ▄▀██▀█ █   █ ██████
  ...
```

## Features

- **All your sessions, everywhere.** Reads Claude Code's on-disk transcripts
  (`~/.claude/projects`) and lists every session, grouped by project, with titles,
  branch, message count and last-activity time.
- **Resume or start fresh.** Open any existing session and keep the conversation going,
  or spin up a new session in any working directory on the host.
- **Live streaming.** Responses stream token-by-token over Server-Sent Events, including
  tool calls and results.
- **Console QR + LAN URL.** Auto-detects your machine's LAN IPv4 and renders a scannable
  QR code straight in the terminal.
- **Keep-awake.** Launch with `--no-sleep` / `-ns` to stop the host from sleeping while
  it's serving (Windows / macOS / Linux).
- **Single file.** Ships as one self-contained executable — no runtime install required.
- **Warm by design.** UI built to Anthropic's cream + coral editorial design system.

## Requirements

- The [Claude Code](https://claude.com/claude-code) CLI installed and authenticated on
  the host machine (`claude` on the `PATH`, or `~/.local/bin`).
- To build from source: the [.NET SDK](https://dotnet.microsoft.com/download) (10.0+).

## Run

Download/build the single-file binary for your OS, then:

```bash
# Serve on the default port (8787), all interfaces
headlesscoder

# Keep the machine awake while serving
headlesscoder --no-sleep

# Custom port
headlesscoder --port 9000
```

Then open the printed URL (or scan the QR code) on any device on the same network.

### Options

| Flag | Description |
|------|-------------|
| `-ns`, `--no-sleep` | Prevent the host machine from sleeping while running. |
| `--port <n>` | Port to serve the web UI on (default `8787`). |
| `--bind <addr>` | Address Kestrel binds to (default `0.0.0.0` = all interfaces). |
| `--host <ip>` | LAN IP to advertise in the printed URL/QR (auto-detected otherwise). |
| `-h`, `--help` | Show help. |

### Permission modes

The composer lets you pick how tools are handled per message, mapping to Claude Code's
`--permission-mode`:

- **Ask** (`default`) — tools needing approval are skipped in headless mode.
- **Accept edits** (`acceptEdits`) — auto-approve file edits.
- **Plan** (`plan`) — planning only, no changes.
- **Bypass (YOLO)** (`bypassPermissions`) — run everything. Use with care.

## Build from source

```bash
# Run locally
dotnet run --project src/HeadlessCoder

# Publish a self-contained single-file binary
dotnet publish src/HeadlessCoder -c Release -r win-x64   -o publish/win-x64
dotnet publish src/HeadlessCoder -c Release -r osx-arm64 -o publish/osx-arm64
dotnet publish src/HeadlessCoder -c Release -r linux-x64 -o publish/linux-x64
```

The resulting `headlesscoder[.exe]` is the only file you need to run.

## How it works

```
 phone / laptop                host machine
 ┌───────────┐   HTTP/SSE   ┌──────────────────────────────┐
 │  web UI   │ ───────────► │  HeadlessCoder (ASP.NET Core) │
 │  (/hc)    │ ◄─────────── │   • reads ~/.claude/projects  │
 └───────────┘   stream     │   • spawns `claude --print`   │
                            │       --output-format         │
                            │        stream-json --resume   │
                            └──────────────┬────────────────┘
                                           ▼
                                      Claude Code CLI
```

- **Listing** parses the JSONL transcripts under `~/.claude/projects` directly.
- **Messaging** spawns the `claude` CLI in headless streaming mode
  (`--print --output-format stream-json --include-partial-messages`), assigning a fresh
  `--session-id` for new sessions or `--resume`-ing existing ones, and relays each event
  to the browser as SSE.

## Security note

HeadlessCoder serves on your LAN with **no authentication**, and anyone who can reach the
URL can drive Claude Code on your machine (including running tools). Only run it on trusted
networks. A future version may add a pairing token.

## Project layout

```
src/HeadlessCoder/
  Program.cs                 # host + HTTP/SSE endpoints
  CommandLineOptions.cs      # arg parsing (--no-sleep, --port, ...)
  ConsoleUi.cs               # banner, LAN URL, terminal QR code
  Networking/NetworkHelper   # LAN IPv4 discovery
  Platform/SleepPreventer    # cross-platform keep-awake
  Claude/
    ClaudeSessionStore.cs    # read/parse ~/.claude/projects transcripts
    ClaudeCliRunner.cs       # spawn claude, stream stream-json
    SessionModels.cs         # DTOs
  Hosting/EmbeddedAssets.cs  # serve embedded web UI (single-file friendly)
  Web/                       # embedded UI: index.html, styles.css, app.js
```

## License

MIT
