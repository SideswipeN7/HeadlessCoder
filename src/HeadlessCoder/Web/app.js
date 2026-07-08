"use strict";

// ---- State -----------------------------------------------------------------
const state = {
  current: null,      // { provider, project, id, cwd, title } | null
  sending: false,
  liveEl: null,       // in-progress assistant bubble
  projects: [],       // [{path}]
  agents: [],         // AgentDescriptor[]
  sessions: [],       // cached last session list
  groupMode: "recent" // "recent" | "agent"
};

const $ = (id) => document.getElementById(id);
const app = $("app");

// ---- Boot ------------------------------------------------------------------
window.addEventListener("DOMContentLoaded", () => {
  $("newSessionBtn").addEventListener("click", openNewDialog);
  $("backBtn").addEventListener("click", () => app.classList.remove("show-chat"));
  $("refreshBtn").addEventListener("click", () => state.current
    ? loadTranscript(state.current)
    : loadSessions());
  $("sendBtn").addEventListener("click", send);
  $("input").addEventListener("keydown", onInputKey);
  $("input").addEventListener("input", autoGrow);
  $("newForm").addEventListener("submit", onNewSubmit);

  for (const seg of document.querySelectorAll("#groupToggle .seg")) {
    seg.addEventListener("click", () => setGroupMode(seg.dataset.mode));
  }

  initAppearance();
  loadAgents();
  loadSessions();
});

// ---- Appearance (theme + light/dark) --------------------------------------
const THEMES = ["claude", "github", "openai", "opencode", "obsidian"];

function initAppearance() {
  const root = document.documentElement;
  let theme = root.dataset.theme;
  if (THEMES.indexOf(theme) < 0) theme = "claude";
  let mode = root.dataset.mode === "dark" ? "dark" : "light";
  applyAppearance(theme, mode, false);

  $("themeSelect").value = theme;
  $("themeSelect").addEventListener("change", (e) =>
    applyAppearance(e.target.value, currentMode(), true));
  $("modeToggle").addEventListener("click", () =>
    applyAppearance(currentTheme(), currentMode() === "dark" ? "light" : "dark", true));
}

function currentTheme() { return document.documentElement.dataset.theme || "claude"; }
function currentMode() { return document.documentElement.dataset.mode === "dark" ? "dark" : "light"; }

function applyAppearance(theme, mode, persist) {
  const root = document.documentElement;
  root.dataset.theme = theme;
  root.dataset.mode = mode;
  $("modeToggle").textContent = mode === "dark" ? "☀️" : "🌙";
  $("modeToggle").title = mode === "dark" ? "Switch to light" : "Switch to dark";
  // Keep the browser chrome colour in step with the canvas.
  const bg = getComputedStyle(root).getPropertyValue("--canvas").trim();
  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta && bg) meta.setAttribute("content", bg);
  if (persist) {
    try {
      localStorage.setItem("hc-theme", theme);
      localStorage.setItem("hc-mode", mode);
    } catch (e) { /* ignore */ }
  }
}

function setGroupMode(mode) {
  if (mode === state.groupMode) return;
  state.groupMode = mode;
  for (const seg of document.querySelectorAll("#groupToggle .seg"))
    seg.classList.toggle("active", seg.dataset.mode === mode);
  renderSessions(state.sessions);
}

// ---- Agents / doctor -------------------------------------------------------
async function loadAgents() {
  try {
    state.agents = await (await fetch("/api/agents")).json();
  } catch {
    state.agents = [];
  }
  renderDoctor();
  renderAgentPicker();
  renderStatusFoot();
}

function agentById(id) { return state.agents.find((a) => a.id === id); }
function installedAgents() { return state.agents.filter((a) => a.installed); }
function agentName(id) { const a = agentById(id); return a ? a.displayName : id; }

function renderStatusFoot() {
  const ready = installedAgents().map((a) => a.displayName);
  $("statusFoot").textContent = ready.length
    ? `agents: ${ready.join(", ")}`
    : "⚠ no agent CLI detected";
}

function renderDoctor() {
  const note = $("doctorNote");
  const missing = state.agents.filter((a) => !a.installed);
  const partial = state.agents.filter((a) => a.installed && a.status === "partial");
  if (!missing.length && !partial.length) { note.hidden = true; return; }

  const rows = [];
  if (!installedAgents().length) {
    rows.push(`<b>No agent CLI found.</b> Install at least one:`);
  }
  for (const a of [...partial, ...missing]) {
    rows.push(`• <b>${escapeHtml(a.displayName)}</b> — ${escapeHtml(a.remediation || (a.installed ? "needs setup" : "not installed"))}`);
  }
  note.innerHTML = rows.join("<br/>");
  note.hidden = false;
}

function renderAgentPicker() {
  const sel = $("newAgent");
  sel.innerHTML = "";
  const list = state.agents.length ? state.agents : [{ id: "claude", displayName: "Claude Code", installed: true }];
  for (const a of list) {
    const o = document.createElement("option");
    o.value = a.id;
    o.textContent = a.installed ? a.displayName : `${a.displayName} (not installed)`;
    o.disabled = !a.installed;
    sel.appendChild(o);
  }
  const firstInstalled = list.find((a) => a.installed);
  if (firstInstalled) sel.value = firstInstalled.id;
}

// ---- Session list ----------------------------------------------------------
async function loadSessions() {
  const list = $("sessionList");
  try {
    const [sessions, projects] = await Promise.all([
      fetch("/api/sessions").then((r) => r.json()),
      fetch("/api/projects").then((r) => r.json()),
    ]);
    state.sessions = sessions;
    state.projects = projects;
    renderProjectDatalist(projects);
    renderSessions(sessions);
  } catch {
    list.innerHTML = `<div class="empty muted">Failed to load sessions.</div>`;
  }
}

function renderSessions(sessions) {
  const list = $("sessionList");
  if (!sessions || !sessions.length) {
    list.innerHTML = `<div class="empty muted">No sessions yet.<br/>Start a new one above.</div>`;
    return;
  }
  list.innerHTML = "";

  if (state.groupMode === "agent") {
    // Group by agent/provider.
    const groups = new Map();
    for (const s of sessions) {
      const k = s.provider || "claude";
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k).push(s);
    }
    for (const [prov, items] of groups)
      list.appendChild(group(agentName(prov), items, false));
  } else {
    // Recent: flat, newest first (already sorted server-side), show project path.
    for (const s of sessions) list.appendChild(sessionItem(s, true));
  }
}

function group(headText, items, showAgent) {
  const g = document.createElement("div");
  g.className = "project-group";
  const head = document.createElement("div");
  head.className = "project-head";
  head.textContent = headText;
  head.title = headText;
  g.appendChild(head);
  for (const s of items) g.appendChild(sessionItem(s, showAgent));
  return g;
}

function sessionItem(s, showAgent) {
  const btn = document.createElement("button");
  btn.className = "session-item";
  btn.dataset.id = s.id;
  btn.dataset.provider = s.provider || "claude";
  if (state.current && state.current.id === s.id && state.current.provider === s.provider)
    btn.classList.add("active");

  const meta = [];
  if (s.messageCount) meta.push(`${s.messageCount} msgs`);
  if (s.lastActivity) meta.push(relTime(s.lastActivity));

  const prov = s.provider || "claude";
  btn.innerHTML = `
    <div class="s-title"></div>
    <div class="s-meta">
      ${showAgent ? `<span class="agent-badge ${escapeAttr(prov)}"></span>` : ""}
      ${s.gitBranch ? `<span class="branch-pill"></span>` : ""}
      <span class="s-metatext"></span>
    </div>`;
  btn.querySelector(".s-title").textContent = s.title;
  btn.querySelector(".s-metatext").textContent = meta.join(" · ");
  if (showAgent) btn.querySelector(".agent-badge").textContent = agentName(prov);
  if (s.gitBranch) btn.querySelector(".branch-pill").textContent = s.gitBranch;

  btn.addEventListener("click", () =>
    openSession({ provider: prov, project: s.projectId, id: s.id, cwd: s.cwd, title: s.title }));
  return btn;
}

// ---- Open / render a session ----------------------------------------------
async function openSession(sess) {
  state.current = sess;
  markActive();
  $("chatTitle").textContent = sess.title || "Session";
  $("chatSub").textContent = `${agentName(sess.provider)} · ${shortenPath(sess.cwd)}`;
  $("cwdChip").textContent = shortenPath(sess.cwd);
  $("composer").hidden = false;
  app.classList.add("show-chat");
  await loadTranscript(sess);
}

async function loadTranscript(sess) {
  const t = $("transcript");
  t.innerHTML = `<div class="empty muted">Loading transcript…</div>`;
  try {
    const supportsHistory = (agentById(sess.provider) || {}).supportsHistory !== false;
    const msgs = (sess.id && supportsHistory)
      ? await fetch(`/api/sessions/${encodeURIComponent(sess.provider)}/${encodeURIComponent(sess.project)}/${encodeURIComponent(sess.id)}`).then((r) => r.json())
      : [];
    t.innerHTML = "";
    if (!msgs.length) addWelcomeInline(sess);
    else for (const m of msgs) renderTranscriptMessage(m);
    scrollBottom();
  } catch {
    t.innerHTML = `<div class="empty muted">Failed to load transcript.</div>`;
  }
}

function addWelcomeInline(sess) {
  const d = document.createElement("div");
  d.className = "result-line";
  const noHist = (agentById(sess.provider) || {}).supportsHistory === false;
  d.textContent = noHist
    ? `${agentName(sess.provider)} — one-shot mode (no stored history). Send a message to run it.`
    : "New session — send a message to begin.";
  $("transcript").appendChild(d);
}

function renderTranscriptMessage(m) {
  if (m.role === "user") return addUserBubble(m.text);
  if (m.role === "assistant") return addAssistantBubble(m.text);
  if (m.role === "tool") return addToolChip(m.toolName || "tool", m.text);
}

// ---- Composing / sending ---------------------------------------------------
function onInputKey(e) {
  if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); }
}
function autoGrow() {
  const el = $("input");
  el.style.height = "auto";
  el.style.height = Math.min(el.scrollHeight, 200) + "px";
}

async function send() {
  if (state.sending || !state.current) return;
  const input = $("input");
  const text = input.value.trim();
  if (!text) return;

  input.value = "";
  autoGrow();
  addUserBubble(text);
  scrollBottom();

  state.sending = true;
  setSending(true);

  const body = {
    provider: state.current.provider || "claude",
    sessionId: state.current.id || null,
    cwd: state.current.cwd,
    message: text,
    permissionMode: $("permMode").value,
    model: $("model").value || null,
  };

  try {
    await streamMessage(body);
  } catch (e) {
    addErrorLine(String(e && e.message ? e.message : e));
  } finally {
    state.sending = false;
    setSending(false);
    endLive();
    loadSessions();
  }
}

async function streamMessage(body) {
  const resp = await fetch("/api/message", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!resp.ok || !resp.body) throw new Error(`HTTP ${resp.status}`);

  const reader = resp.body.getReader();
  const decoder = new TextDecoder();
  let buf = "";
  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buf += decoder.decode(value, { stream: true });
    let idx;
    while ((idx = buf.indexOf("\n\n")) >= 0) {
      handleSse(buf.slice(0, idx));
      buf = buf.slice(idx + 2);
    }
  }
}

function handleSse(frame) {
  let event = "agent";
  const dataLines = [];
  for (const line of frame.split("\n")) {
    if (line.startsWith("event:")) event = line.slice(6).trim();
    else if (line.startsWith("data:")) dataLines.push(line.slice(5).replace(/^ /, ""));
  }
  const data = dataLines.join("\n");

  if (event === "meta") {
    try {
      const meta = JSON.parse(data);
      if (meta.sessionId && state.current) {
        if (meta.isNew) state.current.id = meta.sessionId;
        if (meta.provider) state.current.provider = meta.provider;
      }
    } catch { /* ignore */ }
    return;
  }
  if (event === "done") { endLive(); return; }

  // event === "agent": a normalized AgentEvent
  let ev;
  try { ev = JSON.parse(data); } catch { return; }
  dispatchAgentEvent(ev);
}

function dispatchAgentEvent(ev) {
  switch (ev.kind) {
    case "system":
      if (ev.sessionId && state.current) state.current.id = ev.sessionId;
      break;
    case "text_delta":
      ensureLive().textContent += ev.text || "";
      scrollBottom();
      break;
    case "assistant":
      if (ev.text && ev.text.trim()) finalizeAssistantText(ev.text);
      break;
    case "tool":
      endLive();
      addToolChip(ev.toolName || "tool", ev.text || "");
      break;
    case "tool_result":
      addToolChip(ev.toolName || "result", ev.text || "");
      break;
    case "result":
      endLive();
      addResultLine(ev);
      break;
    case "error":
      endLive();
      addErrorLine(ev.message || "unknown error");
      break;
  }
}

// ---- Bubble builders -------------------------------------------------------
function addUserBubble(text) {
  const d = document.createElement("div");
  d.className = "msg msg-user";
  d.textContent = text;
  $("transcript").appendChild(d);
}
function addAssistantBubble(text) {
  const wrap = document.createElement("div");
  wrap.className = "msg msg-assistant";
  wrap.innerHTML = `<div class="msg-role"></div>`;
  wrap.querySelector(".msg-role").textContent = state.current ? agentName(state.current.provider) : "Assistant";
  const body = document.createElement("div");
  body.textContent = text;
  wrap.appendChild(body);
  $("transcript").appendChild(wrap);
}
function addToolChip(name, detail) {
  const d = document.createElement("div");
  d.className = "tool";
  d.innerHTML = `<span class="tool-name"></span>`;
  d.querySelector(".tool-name").textContent = "🔧 " + name;
  if (detail && detail.trim()) {
    const pre = document.createElement("pre");
    pre.textContent = detail;
    d.appendChild(pre);
  }
  $("transcript").appendChild(d);
}
function addResultLine(ev) {
  const parts = [];
  if (typeof ev.durationMs === "number") parts.push(`${(ev.durationMs / 1000).toFixed(1)}s`);
  if (typeof ev.costUsd === "number" && ev.costUsd > 0) parts.push(`$${ev.costUsd.toFixed(4)}`);
  if (ev.turns) parts.push(`${ev.turns} turns`);
  if (ev.isError) parts.push("error");
  const d = document.createElement("div");
  d.className = "result-line";
  d.textContent = parts.length ? `— ${parts.join(" · ")} —` : "— done —";
  $("transcript").appendChild(d);
  scrollBottom();
}
function addErrorLine(text) {
  const d = document.createElement("div");
  d.className = "error-line";
  d.textContent = text;
  $("transcript").appendChild(d);
  scrollBottom();
}

function ensureLive() {
  if (state.liveEl) return state.liveEl;
  const wrap = document.createElement("div");
  wrap.className = "msg msg-assistant live";
  wrap.innerHTML = `<div class="msg-role"></div>`;
  wrap.querySelector(".msg-role").textContent = state.current ? agentName(state.current.provider) : "Assistant";
  const body = document.createElement("div");
  wrap.appendChild(body);
  $("transcript").appendChild(wrap);
  state.liveEl = body;
  return body;
}
function endLive() {
  if (state.liveEl) {
    if (!state.liveEl.textContent.trim()) state.liveEl.parentElement.remove();
    else state.liveEl.parentElement.classList.remove("live");
    state.liveEl = null;
  }
}
function finalizeAssistantText(text) {
  if (state.liveEl) {
    state.liveEl.textContent = text;
    state.liveEl.parentElement.classList.remove("live");
    state.liveEl = null;
  } else {
    addAssistantBubble(text);
  }
}

// ---- New session dialog ----------------------------------------------------
function openNewDialog() {
  renderAgentPicker();
  if (state.current) {
    $("newCwd").value = state.current.cwd;
    if (state.current.provider) $("newAgent").value = state.current.provider;
  } else {
    $("newCwd").value = state.projects[0]?.path || "";
  }
  $("newDialog").showModal();
}
function renderProjectDatalist(projects) {
  const dl = $("projectPaths");
  dl.innerHTML = "";
  for (const p of projects) {
    const o = document.createElement("option");
    o.value = p.path;
    dl.appendChild(o);
  }
}
function onNewSubmit(e) {
  const btn = e.submitter;
  if (!btn || btn.value !== "ok") return;
  const cwd = $("newCwd").value.trim();
  const provider = $("newAgent").value || "claude";
  if (!cwd) { e.preventDefault(); return; }
  openSession({ provider, project: null, id: null, cwd, title: "New session" });
  $("transcript").innerHTML = "";
  addWelcomeInline({ provider });
  setTimeout(() => $("input").focus(), 50);
}

// ---- Helpers ---------------------------------------------------------------
function markActive() {
  for (const el of document.querySelectorAll(".session-item"))
    el.classList.toggle("active", state.current &&
      el.dataset.id === state.current.id && el.dataset.provider === state.current.provider);
}
function setSending(on) {
  $("sendBtn").disabled = on;
  $("sendBtn").textContent = on ? "…" : "Send";
}
function scrollBottom() {
  const t = $("transcript");
  t.scrollTop = t.scrollHeight;
}
function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
function escapeAttr(s) { return String(s).replace(/[^a-z0-9_-]/gi, ""); }
function shortenPath(p) {
  const parts = String(p || "").split(/[\\/]/).filter(Boolean);
  return parts.length <= 2 ? p : "…/" + parts.slice(-2).join("/");
}
function relTime(iso) {
  const then = new Date(iso).getTime();
  if (isNaN(then)) return "";
  const s = Math.max(1, Math.floor((Date.now() - then) / 1000));
  if (s < 60) return `${s}s ago`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}
