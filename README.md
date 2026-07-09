<p align="center">
  <img src="logo.svg" alt="HeadlessCoder" width="300" />
</p>

<h1 align="center">HeadlessCoder</h1>

Drive your coding-agent CLIs — **Claude Code**, **Antigravity CLI**, **Copilot CLI** — from your
phone, or any device on your LAN.

HeadlessCoder runs on the machine where your agent CLIs live, serves a small web UI, and
prints a **URL + QR code** in the console. On startup it runs a **preflight** that detects
which agents are installed and tells you exactly what to install/set up for the ones that
aren't. Scan the QR from your phone and you get a warm, editorial interface to browse every
session on the machine, resume past sessions (Claude, Copilot, Qwen), and start new ones on any
agent — with live streaming responses.

```
  ✳  HeadlessCoder  ·  manage your coding agents from anywhere on your LAN

  Preflight — agent CLIs on this machine:
    ✓ Claude Code — 2.1.198 (Claude Code)  ·  14 sessions
    ○ Antigravity CLI — not installed
        ↳ Install the Antigravity CLI (`agy`) from https://antigravity.google, then run `agy` once to authenticate.
    ○ Copilot CLI — not installed
        ↳ Install with `npm install -g @github/copilot`, then run `copilot` once and `/login`.

  Open this on your phone or any device on the same network:
      http://192.168.100.1:8787

  █████████████████████████████████████
  ██████ ▄▄▄▄▄ ██▄▄ ▄▄▄▀██ ▄▄▄▄▄ ██████
  ...
```

## Features

- **Multi-agent.** Provider abstraction over multiple agent CLIs. Detects each one and
  routes messages to it. Claude Code is fully wired (history + resume + streaming + tools);
  **Copilot** and **Qwen** expose readable session history; the rest run as one-shot headless
  prompts with streamed output.
- **Per-agent controls.** The composer's Mode / Model / Effort section rebuilds itself for the
  selected agent — each shows only the working modes and models it actually supports (e.g.
  Copilot offers GPT-5 / Claude Sonnet, Claude offers Opus / Sonnet / Haiku + effort).
- **Startup preflight / doctor.** Reports which agent CLIs are installed, their versions and
  session counts, and precise remediation for anything missing — in the console *and* the UI.
- **All your sessions, everywhere.** Reads on-disk transcripts — Claude
  (`~/.claude/projects`), Copilot (`~/.copilot/session-state`), Qwen (`~/.qwen/tmp`) — and
  lists every session with title, branch, message count and last-activity time. Group them
  **by most recent** or **by agent**.
- **Usage & context at a glance.** After a Claude turn — and the moment you open a Claude
  chat — the composer shows a context-window meter and a usage popover (tokens in/out, cache,
  cost). (The 5-hour rolling limit isn't exposed by the Claude CLI, so it can't be shown.)
- **Resume or start fresh.** Open any existing session and keep going, or spin up a new one
  on any installed agent in any working directory on the host.
- **Live streaming.** Responses stream token-by-token over Server-Sent Events (normalized so
  the UI is agent-agnostic), including tool calls and results.
- **Console QR + LAN URL.** Auto-detects your machine's LAN IPv4 and renders a scannable QR
  code straight in the terminal.
- **Keep-awake.** Launch with `--no-sleep` / `-ns` to stop the host from sleeping while it's
  serving (Windows / macOS / Linux).
- **Password out of the box.** Access is protected by a password generated from Transformers
  names (or your own via `--pass`, or off with `--no-pass`). The QR embeds it for one-scan sign-in.
- **Settings modal.** The ⚙️ in the header opens Appearance (color scheme, light/dark, hand-tuned
  colors), Agents (which CLIs are active, with Refresh), Config (the effective runtime settings plus
  a scannable access QR), Help, and About — all in one place.
- **Startup logo.** Prints an ASCII-art logo on launch (suppress with `--no-logo` / `-nl`); framework
  logging is off by default and can be turned on with `--logs` / `-l`.
- **About + update check.** The About tab shows the version and can check GitHub Releases for a
  newer one.
- **Markdown replies.** Assistant output renders as Markdown — headings, lists, tables, and code
  blocks — like Claude Desktop. Tool calls show a summary + pretty-printed input.
- **Rename sessions.** ✎ (or double-click the title) gives a session a custom name, stored
  separately from the agent's transcript; clearing it restores the original.
- **Share links.** 🔗 copies a link that reopens the current conversation on any device.
- **Shift+Tab** cycles the working mode (Ask → Accept edits → Plan → Bypass) from the composer.
- **Effort control.** Pick the reasoning effort per message (Claude: low → max), with an ⓘ
  popover explaining each level.
- **Non-blocking, queued sends.** Sending never locks the composer — type and submit more
  while a reply streams; the queue drains sequentially into the **same session**.
- **Paste images.** Paste an image into the composer to attach it; it uploads to the host and
  rides along in the message so the agent can read it. Click any thumbnail to view it fullscreen.
- **Roomy input.** The message box starts at ~5 lines and can be expanded further (⤢) or dragged.
- **In-browser terminal.** Start with `--commands-allowed` / `-ca` to get a terminal docked
  under the composer: run shell commands with **live** streaming output and ↑/↓ command history,
  minimize it under the input, scoped to the session's working directory.
- **In-private sessions.** Start a session that stays out of the list, can be **minimized** to a
  pill and restored — and whose transcript is **deleted from history** when you close it.
- **Collapsible groups.** In "By agent" view the agent groups collapse; state is remembered.
- **Switchable themes + light/dark.** Pick a UI style by name — **Claude**, **GitHub**,
  **OpenAI**, **opencode**, **Obsidian** — each with a light and dark variant. Your choice is
  remembered in the browser (`localStorage`) and applied before first paint (no flash).
- **Single file.** Ships as one self-contained executable — no runtime install required.

## Supported agents

| Agent | Detect | History / resume | Send message |
|-------|:------:|:----------------:|--------------|
| **Claude Code** (`claude`) | ✅ | ✅ reads `~/.claude/projects`, resumes by id | ✅ `claude --print --output-format stream-json` |
| **Antigravity CLI** (`agy`) | ✅ | — | ✅ one-shot `agy --prompt` |
| **Copilot CLI** (`copilot`) | ✅ | ✅ reads `~/.copilot/session-state` | ✅ one-shot `copilot --prompt` |
| **Codex CLI** (`codex`) | ✅ | — | ✅ one-shot `codex exec` |
| **opencode** (`opencode`) | ✅ | — | ✅ one-shot `opencode run` |
| **Cursor Agent** (`cursor-agent`) | ✅ | — | ✅ one-shot `cursor-agent -p` |
| **Aider** (`aider`) | ✅ | — | ✅ one-shot `aider --message` |
| **Qwen Code** (`qwen`) | ✅ | ✅ reads `~/.qwen/tmp/*/chats` | ✅ one-shot `qwen --prompt` |

Detection is verified for all; Claude is fully wired end-to-end. The other CLIs run as
one-shot headless prompts with stdout streamed to the UI — their exact flags are best-effort
until the CLI is installed and confirmed. Adding another agent is a matter of implementing
`IAgentProvider` (or subclassing `GenericCliProvider`) and registering it — see
`src/HeadlessCoder/Agents/`.

## Requirements

- At least one supported agent CLI installed and authenticated on the host machine
  (on `PATH`, or a common location like `~/.local/bin`). The preflight tells you what's
  missing and how to fix it.
- To build from source: the [.NET SDK](https://dotnet.microsoft.com/download) (10.0+).

## Install

One line — no runtime to install, the binary is self-contained. It drops `headlesscoder`
and an `hc` shortcut onto your machine.

**Linux / macOS**

```sh
curl -fsSL https://raw.githubusercontent.com/SideswipeN7/HeadlessCoder/main/install.sh | sh
```

**Windows (PowerShell)**

```powershell
irm https://raw.githubusercontent.com/SideswipeN7/HeadlessCoder/main/install.ps1 | iex
```

Then run `headlesscoder --no-sleep` (or just `hc`).

The installers pull the latest [release](https://github.com/SideswipeN7/HeadlessCoder/releases)
for your OS/arch. Handy environment overrides:

| Variable | Effect |
|----------|--------|
| `HC_VERSION` | Install a specific tag, e.g. `HC_VERSION=v1.2.0`. |
| `HC_INSTALL_DIR` | Install location (default `~/.local/bin`, or `%LOCALAPPDATA%\Programs\HeadlessCoder` on Windows). |

> Prefer not to pipe to a shell? Download the matching binary from the
> [Releases page](https://github.com/SideswipeN7/HeadlessCoder/releases), rename it to
> `headlesscoder`, and put it on your `PATH`.

Releases are built automatically by the [`release`](.github/workflows/release.yml) workflow
for `linux-x64/arm64`, `osx-x64/arm64`, and `win-x64/arm64` whenever a `vX.Y.Z` tag is pushed.

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
| `-np`, `--no-pass` | Serve without a password (open access on your LAN). |
| `--pass <value>` | Protect access with your own password. |
| `-fs`, `--free-style` | Allow new sessions in **any** folder (default: only existing projects). |
| `--no-history` | Don't read past sessions/transcripts from disk. |
| `-ca`, `--commands-allowed` | Expose an in-browser terminal to run shell commands (use with care). |
| `-nl`, `--no-logo` | Don't print the ASCII-art logo at startup. |
| `-l`, `--logs` | Enable framework logging output (suppressed by default). |
| `-p`, `--port <n>` | Port to serve the web UI on (default `8787`). |
| `--bind <addr>` | Address Kestrel binds to (default `0.0.0.0` = all interfaces). |
| `--host <ip>` | LAN IP to advertise in the printed URL/QR (auto-detected otherwise). |
| `-h`, `--help` | Show help (also available as a Help panel in the UI). |

### Access & password

By default HeadlessCoder protects access with a password **generated from Transformers
names** (e.g. `Grapple-Brawn-Jetfire-67`) and printed at startup. The **QR code embeds it**,
so scanning from your phone signs you in automatically; if you open the URL by hand you enter
the password on a login screen (a session cookie remembers you afterwards).

- `--pass "my-secret"` — use your own password instead of the generated one.
- `--no-pass` — disable it entirely (open access — only on trusted networks).

The Settings panel (⚙️ in the sidebar) shows which agent CLIs are active and whether access is
password-protected, with a **Refresh** to re-detect after installing a CLI.

### Permission modes

The composer lets you pick how tools are handled per message, mapping to Claude Code's
`--permission-mode`:

- **Ask** (`default`) — tools needing approval are skipped in headless mode.
- **Accept edits** (`acceptEdits`) — auto-approve file edits.
- **Plan** (`plan`) — planning only, no changes.
- **Bypass (YOLO)** (`bypassPermissions`) — run everything. Use with care.

The available modes depend on the agent — the composer rebuilds them per provider. Non-Claude
CLIs typically expose just **Default** and **Auto-approve all** (mapped to their own bypass flag,
e.g. `--allow-all-tools` / `--yes` / `--yolo`).

## Themes & appearance

Use the palette control at the bottom of the sidebar to switch the **UI style** and toggle
**light/dark**. Both are stored per-browser in `localStorage` (`hc-theme`, `hc-mode`) and
re-applied before the page paints, so a phone that opened the UI once keeps its look.

**Custom colors.** The 🎚️ button opens a color editor: tweak any of the core tokens
(primary, background, text, borders, accents, …) with live preview. Edits are scoped to the
current style + light/dark combination and saved per-browser (`hc-custom`), so each theme can
carry its own tweaks; **Reset to theme** clears them. Custom overrides are also re-applied
before first paint (no flash).

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

### Cutting a release

Push a semver tag and CI builds every platform binary and attaches it to the release
(which the install scripts then fetch):

```bash
git tag v1.0.0
git push origin v1.0.0
```

## How it works

```
 phone / laptop                host machine
 ┌───────────┐   HTTP/SSE   ┌───────────────────────────────────┐
 │  web UI   │ ───────────► │  HeadlessCoder (ASP.NET Core)      │
 │  (root)   │ ◄─────────── │   AgentRegistry                    │
 └───────────┘  normalized  │    ├─ ClaudeProvider  ── claude    │
                  events     │    ├─ AntigravityProvider ─ agy     │
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
- **Extras** — opening a chat pulls the session's last usage from disk
  (`GET …/usage`); with `--commands-allowed` an SSE terminal endpoint (`/api/terminal`) streams
  a shell command's output live; pasted images upload to a temp file via `/api/upload`.

### Adding an agent

Implement `IAgentProvider` (or subclass `GenericCliProvider` for a one-shot CLI) in
`src/HeadlessCoder/Agents/`, then register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<IAgentProvider, MyAgentProvider>();
```

## Security note

By default access requires the startup password (a session cookie is set after the first
sign-in or QR scan). Traffic is plain HTTP on your LAN, so treat it as trusted-network only:
anyone with the URL **and** the password can drive your agent CLIs (including running tools).
`--no-pass` removes the password entirely — use it only on networks you trust.

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
    AgentModels.cs           # AgentEvent, AgentDescriptor, AgentOption, DoctorReport
    AgentRegistry.cs         # detection + session aggregation + routing
    Cli.cs                   # locate executables, probe versions
    ClaudeProvider.cs        # full Claude Code backend (maps stream-json + usage)
    GenericCliProvider.cs    # base for one-shot prompt CLIs (+ per-agent capabilities)
    AntigravityProvider.cs   # Antigravity CLI (agy)
    CopilotProvider.cs       # Copilot CLI (+ history)
    MoreProviders.cs         # Codex, opencode, Cursor, Aider, Qwen, DeepSeek
    CliHistoryStore.cs       # ICliHistoryStore + Gemini-family (Qwen) session reader
    CopilotSessionStore.cs   # read ~/.copilot/session-state transcripts
  Claude/
    ClaudeSessionStore.cs    # read/parse ~/.claude/projects transcripts (+ last usage)
    ClaudeCliRunner.cs       # spawn claude, stream stream-json
    SessionModels.cs         # DTOs (SessionSummary, TranscriptMessage, AgentUsage, …)
  Terminal/CommandRunner.cs  # spawn a shell command, stream stdout/stderr chunks live
  Hosting/EmbeddedAssets.cs  # serve embedded web UI (single-file friendly)
  Web/                       # embedded UI: index.html, styles.css, themes.css, app.js
```

## License

MIT
