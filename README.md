# ✳ HeadlessCoder

Drive your coding-agent CLIs — **Claude Code**, **Gemini CLI**, **Copilot CLI** — from your
phone, or any device on your LAN.

HeadlessCoder runs on the machine where your agent CLIs live, serves a small web UI, and
prints a **URL + QR code** in the console. On startup it runs a **preflight** that detects
which agents are installed and tells you exactly what to install/set up for the ones that
aren't. Scan the QR from your phone and you get a warm, editorial interface to browse every
session on the machine, resume Claude sessions, and start new ones on any agent — with live
streaming responses.

```
  ✳  HeadlessCoder  ·  manage your coding agents from anywhere on your LAN

  Preflight — agent CLIs on this machine:
    ✓ Claude Code — 2.1.198 (Claude Code)  ·  14 sessions
    ○ Gemini CLI — not installed
        ↳ Install with `npm install -g @google/gemini-cli`, then run `gemini` once to authenticate.
    ○ Copilot CLI — not installed
        ↳ Install with `npm install -g @github/copilot`, then run `copilot` once and `/login`.

  Open this on your phone or any device on the same network:
      http://192.168.100.1:8787/hc

  █████████████████████████████████████
  ██████ ▄▄▄▄▄ ██▄▄ ▄▄▄▀██ ▄▄▄▄▄ ██████
  ...
```

## Features

- **Multi-agent.** Provider abstraction over multiple agent CLIs. Detects each one and
  routes messages to it. Claude Code is fully wired (history + resume + streaming + tools);
  Gemini/Copilot run as one-shot headless prompts with streamed output.
- **Startup preflight / doctor.** Reports which agent CLIs are installed, their versions and
  session counts, and precise remediation for anything missing — in the console *and* the UI.
- **All your sessions, everywhere.** Reads Claude Code's on-disk transcripts
  (`~/.claude/projects`) and lists every session with title, branch, message count and
  last-activity time. Group them **by most recent** or **by agent**.
- **Resume or start fresh.** Open any existing session and keep going, or spin up a new one
  on any installed agent in any working directory on the host.
- **Live streaming.** Responses stream token-by-token over Server-Sent Events (normalized so
  the UI is agent-agnostic), including tool calls and results.
- **Console QR + LAN URL.** Auto-detects your machine's LAN IPv4 and renders a scannable QR
  code straight in the terminal.
- **Keep-awake.** Launch with `--no-sleep` / `-ns` to stop the host from sleeping while it's
  serving (Windows / macOS / Linux).
- **Switchable themes + light/dark.** Pick a UI style by name — **Claude**, **GitHub**,
  **OpenAI**, **opencode**, **Obsidian** — each with a light and dark variant. Your choice is
  remembered in the browser (`localStorage`) and applied before first paint (no flash).
- **Single file.** Ships as one self-contained executable — no runtime install required.

## Supported agents

| Agent | Detect | History / resume | Send message |
|-------|:------:|:----------------:|--------------|
| **Claude Code** (`claude`) | ✅ | ✅ reads `~/.claude/projects`, resumes by id | ✅ `claude --print --output-format stream-json` |
| **Gemini CLI** (`gemini`) | ✅ | — | ✅ one-shot `gemini --prompt` (stdout streamed) |
| **Copilot CLI** (`copilot`) | ✅ | — | ✅ one-shot `copilot --prompt` (stdout streamed) |

Adding another agent is a matter of implementing `IAgentProvider` (or subclassing
`GenericCliProvider`) and registering it — see `src/HeadlessCoder/Agents/`.

## Requirements

- At least one supported agent CLI installed and authenticated on the host machine
  (on `PATH`, or a common location like `~/.local/bin`). The preflight tells you what's
  missing and how to fix it.
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

## Themes & appearance

Use the palette control at the bottom of the sidebar to switch the **UI style** and toggle
**light/dark**. Both are stored per-browser in `localStorage` (`hc-theme`, `hc-mode`) and
re-applied before the page paints, so a phone that opened the UI once keeps its look.

Each style is a set of CSS variables in `Web/themes.css`, selected by `data-theme` +
`data-mode` on `<html>`; palettes are derived from the `*-DESIGN.md` reference docs. To add a
style, append a `:root[data-theme="mystyle"]` block (with `[data-mode="light"]`/`[dark]`
variants) and add an `<option>` to the theme picker in `Web/index.html`.

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
 ┌───────────┐   HTTP/SSE   ┌───────────────────────────────────┐
 │  web UI   │ ───────────► │  HeadlessCoder (ASP.NET Core)      │
 │  (/hc)    │ ◄─────────── │   AgentRegistry                    │
 └───────────┘  normalized  │    ├─ ClaudeProvider  ── claude    │
                  events     │    ├─ GeminiProvider  ── gemini    │
                            │    └─ CopilotProvider ── copilot   │
                            └───────────────┬───────────────────┘
                                            ▼
                                    agent CLI process
```

- **Preflight** — each provider's `Detect()` locates its executable, probes `--version`,
  checks its config/session store, and reports status + remediation. Aggregated into a
  `DoctorReport` shown in the console and at `GET /api/agents`.
- **Listing** — `ClaudeProvider` parses the JSONL transcripts under `~/.claude/projects`.
  Providers without a readable history contribute nothing to the list (by design).
- **Messaging** — the chosen provider spawns its CLI and emits **normalized `AgentEvent`s**
  (`text_delta`, `assistant`, `tool`, `tool_result`, `result`, `error`) so the UI never
  needs to know a CLI's native output format. Claude is driven with
  `--print --output-format stream-json --include-partial-messages`, a fresh `--session-id`
  for new sessions or `--resume` for existing ones; generic CLIs get `--prompt` and their
  stdout is streamed as text.

### Adding an agent

Implement `IAgentProvider` (or subclass `GenericCliProvider` for a one-shot CLI) in
`src/HeadlessCoder/Agents/`, then register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<IAgentProvider, MyAgentProvider>();
```

## Security note

HeadlessCoder serves on your LAN with **no authentication**, and anyone who can reach the
URL can drive your agent CLIs on your machine (including running tools). Only run it on
trusted networks. A future version may add a pairing token.

## Project layout

```
src/HeadlessCoder/
  Program.cs                 # host + HTTP/SSE endpoints, provider registration
  CommandLineOptions.cs      # arg parsing (--no-sleep, --port, ...)
  ConsoleUi.cs               # banner, preflight/doctor, LAN URL, terminal QR
  Networking/NetworkHelper   # LAN IPv4 discovery
  Platform/SleepPreventer    # cross-platform keep-awake
  Agents/
    IAgentProvider.cs        # the agent-backend contract
    AgentModels.cs           # AgentEvent, AgentDescriptor, DoctorReport
    AgentRegistry.cs         # detection + session aggregation + routing
    Cli.cs                   # locate executables, probe versions
    ClaudeProvider.cs        # full Claude Code backend (maps stream-json)
    GenericCliProvider.cs    # base for one-shot prompt CLIs
    GeminiProvider.cs        # Gemini CLI
    CopilotProvider.cs       # Copilot CLI
  Claude/
    ClaudeSessionStore.cs    # read/parse ~/.claude/projects transcripts
    ClaudeCliRunner.cs       # spawn claude, stream stream-json
    SessionModels.cs         # DTOs
  Hosting/EmbeddedAssets.cs  # serve embedded web UI (single-file friendly)
  Web/                       # embedded UI: index.html, styles.css, themes.css, app.js
```

## License

MIT
