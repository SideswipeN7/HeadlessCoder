"use strict";

// ---- State -----------------------------------------------------------------
const state = {
  current: null,      // { provider, project, id, cwd, title } | null
  sending: false,
  liveEl: null,       // in-progress assistant bubble
  projects: [],       // [{path}]
  agents: [],         // AgentDescriptor[]
  sessions: [],       // cached last session list
  groupMode: "recent", // "recent" | "agent"
  collapsed: new Set(), // collapsed group keys
  config: {},          // { auth, freeStyle, noHistory } from /api/health
  privateIds: new Set(), // in-private session ids (kept out of the list)
  minimized: []        // minimized in-private sessions
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
  $("expandBtn").addEventListener("click", toggleExpand);
  $("terminalBtn").addEventListener("click", toggleTerminal);
  $("terminalForm").addEventListener("submit", runTerminalCommand);
  $("terminalMinBtn").addEventListener("click", (e) => { e.stopPropagation(); minimizeTerminal(); });
  $("terminalCloseBtn").addEventListener("click", (e) => { e.stopPropagation(); $("terminalWindow").hidden = true; });
  $("terminalClearBtn").addEventListener("click", (e) => { e.stopPropagation(); $("terminalOutput").innerHTML = ""; });
  $("terminalHead").addEventListener("click", restoreTerminal);
  $("newForm").addEventListener("submit", onNewSubmit);

  for (const seg of document.querySelectorAll("#groupToggle .seg")) {
    seg.addEventListener("click", () => setGroupMode(seg.dataset.mode));
  }

  $("gearBtn").addEventListener("click", () => openSettings());
  $("shareBtn").addEventListener("click", shareCurrent);
  $("minimizeBtn").addEventListener("click", minimizeCurrent);
  $("renameBtn").addEventListener("click", openRename);
  $("renameForm").addEventListener("submit", onRenameSubmit);
  $("chatTitle").addEventListener("dblclick", openRename);
  $("agentsRefresh").addEventListener("click", refreshAgents);
  $("checkUpdateBtn").addEventListener("click", checkForUpdate);
  for (const tab of document.querySelectorAll("#settingsTabs .tab"))
    tab.addEventListener("click", () => switchTab(tab.dataset.tab));

  loadCollapsed();
  initAppearance();
  loadConfig();
  loadAgents();
  loadSessions().then(applyDeepLink);

  // Discard in-private transcripts when the page goes away.
  window.addEventListener("pagehide", () => {
    const gone = [...state.minimized];
    if (state.current && state.current.private) gone.push(state.current);
    for (const s of gone) purgePrivate(s);
  });
});

// ---- Config (health) -------------------------------------------------------
async function loadConfig() {
  try { state.config = await (await fetch("/api/health")).json(); }
  catch { state.config = {}; }
  // Reveal the terminal button only when the server allows commands.
  const tbtn = $("terminalBtn");
  if (tbtn) tbtn.hidden = !(state.config && state.config.commandsAllowed);
}

// ---- Settings tabs ---------------------------------------------------------
function switchTab(name) {
  for (const t of document.querySelectorAll("#settingsTabs .tab"))
    t.classList.toggle("active", t.dataset.tab === name);
  for (const p of document.querySelectorAll(".tab-panel"))
    p.classList.toggle("active", p.dataset.panel === name);
}

// ---- Collapsible groups ----------------------------------------------------
function loadCollapsed() {
  try { state.collapsed = new Set(JSON.parse(localStorage.getItem("hc-collapsed") || "[]")); }
  catch { state.collapsed = new Set(); }
}
function toggleCollapsed(key, groupEl) {
  if (state.collapsed.has(key)) state.collapsed.delete(key);
  else state.collapsed.add(key);
  groupEl.classList.toggle("collapsed", state.collapsed.has(key));
  try { localStorage.setItem("hc-collapsed", JSON.stringify([...state.collapsed])); } catch { /* ignore */ }
}

// ---- Settings modal --------------------------------------------------------
function openSettings(tab) {
  $("themeSelect").value = currentTheme();
  updateModeSeg();
  populateCustomFields();
  renderAgentsList();
  renderAccessInfo();
  renderAbout();
  if (tab) switchTab(tab); else switchTab("appearance");
  $("settingsDialog").showModal();
}

// ---- About / updates -------------------------------------------------------
async function renderAbout() {
  try {
    const a = await (await fetch("/api/about")).json();
    $("aboutVersion").textContent = "v" + a.version;
    $("aboutMeta").textContent = `.NET ${a.runtime} · ${a.os}`;
    $("aboutRepo").href = a.repoUrl;
  } catch { $("aboutVersion").textContent = ""; }
}

async function checkForUpdate() {
  const btn = $("checkUpdateBtn");
  const res = $("updateResult");
  btn.disabled = true;
  const label = btn.textContent;
  btn.textContent = "Checking…";
  res.textContent = "";
  res.classList.remove("update-avail");
  try {
    const u = await (await fetch("/api/update-check")).json();
    if (u.error) {
      res.textContent = "⚠ " + u.error;
    } else if (u.updateAvailable) {
      res.classList.add("update-avail");
      res.textContent = `New version ${u.latest} available — `;
      const link = document.createElement("a");
      link.href = u.url || "#";
      link.target = "_blank"; link.rel = "noopener noreferrer";
      link.className = "text-link";
      link.textContent = "download ↗";
      res.appendChild(link);
    } else if (u.note) {
      res.textContent = u.note;
    } else {
      res.textContent = `You're on the latest version (v${u.current}).`;
    }
  } catch {
    res.textContent = "⚠ Update check failed.";
  }
  btn.textContent = label;
  btn.disabled = false;
}
function renderAgentsList() {
  const wrap = $("agentsList");
  wrap.innerHTML = "";
  if (!state.agents.length) {
    wrap.innerHTML = `<div class="muted small">No agents detected.</div>`;
    return;
  }
  for (const a of state.agents) {
    const status = a.status || (a.installed ? "ready" : "missing");
    const sub = a.installed
      ? ([a.version, a.supportsHistory && a.sessionCount ? `${a.sessionCount} sessions` : null]
          .filter(Boolean).join(" · ") || "installed")
      : "not installed — tap for install steps";
    // Wrap the exec path in backticks so it renders as a copyable command chip too.
    const detail = a.installed ? (a.executablePath ? "`" + a.executablePath + "`" : "") : (a.remediation || "");

    const row = document.createElement("div");
    row.className = "agent-row";
    row.innerHTML = `
      <span class="agent-dot ${status}"></span>
      <div class="agent-info">
        <div class="agent-name"></div>
        <div class="agent-sub"></div>
        <div class="agent-detail"></div>
      </div>
      <span class="agent-state ${status}"></span>`;
    row.querySelector(".agent-name").textContent = a.displayName;
    row.querySelector(".agent-sub").textContent = sub;
    if (detail) {
      renderInstallDetail(row.querySelector(".agent-detail"), detail);
      row.addEventListener("click", () => row.classList.toggle("open"));
    } else {
      row.classList.add("no-detail");
    }
    wrap.appendChild(row);
  }
}

// Render an install hint: backtick-wrapped commands become click-to-copy chips.
function renderInstallDetail(el, text) {
  el.innerHTML = "";
  for (const part of text.split(/(`[^`]+`)/)) {
    if (!part) continue;
    if (part.length > 1 && part[0] === "`" && part[part.length - 1] === "`") {
      const cmd = part.slice(1, -1);
      const chip = document.createElement("button");
      chip.type = "button";
      chip.className = "cmd";
      chip.title = "Copy to clipboard";
      chip.innerHTML = `<span class="cmd-text"></span><span class="cmd-copy">⧉</span>`;
      chip.querySelector(".cmd-text").textContent = cmd;
      chip.addEventListener("click", (e) => {
        e.stopPropagation();   // don't collapse the row
        copyText(cmd).then(() => showToast("Copied: " + cmd), () => showToast(cmd));
      });
      el.appendChild(chip);
    } else {
      el.appendChild(document.createTextNode(part));
    }
  }
}
async function refreshAgents() {
  const btn = $("agentsRefresh");
  btn.disabled = true;
  const label = btn.textContent;
  btn.textContent = "…";
  await loadAgents();       // re-runs server-side detection
  await loadSessions();     // pick up sessions from a newly-installed agent
  renderAgentsList();
  renderAccessInfo();
  btn.textContent = label;
  btn.disabled = false;
}
async function renderAccessInfo() {
  try {
    const h = await (await fetch("/api/health")).json();
    $("accessInfo").textContent = h.auth
      ? "Access: 🔒 password protected"
      : "Access: 🔓 open (started with --no-pass)";
  } catch { $("accessInfo").textContent = ""; }
}

// ---- Appearance (theme + light/dark) --------------------------------------
const THEMES = ["claude", "github", "openai", "opencode", "obsidian"];

// Editable colour tokens, shown in the manual customizer.
const CUSTOM_TOKENS = [
  { v: "--primary", label: "Primary" },
  { v: "--primary-active", label: "Primary (active)" },
  { v: "--canvas", label: "Background" },
  { v: "--surface-soft", label: "Sidebar" },
  { v: "--surface-card", label: "Card / bubble" },
  { v: "--surface-cream-strong", label: "Active item" },
  { v: "--ink", label: "Heading text" },
  { v: "--body", label: "Body text" },
  { v: "--muted", label: "Muted text" },
  { v: "--hairline", label: "Border" },
  { v: "--accent-teal", label: "Accent" },
  { v: "--success", label: "Success" },
  { v: "--error", label: "Error" },
  { v: "--surface-dark", label: "Code panel" },
];

function initAppearance() {
  const root = document.documentElement;
  let theme = root.dataset.theme;
  if (THEMES.indexOf(theme) < 0) theme = "claude";
  let mode = root.dataset.mode === "dark" ? "dark" : "light";
  applyAppearance(theme, mode, false);

  $("themeSelect").value = theme;
  $("themeSelect").addEventListener("change", (e) => {
    applyAppearance(e.target.value, currentMode(), true);
    populateCustomFields();
  });
  for (const seg of document.querySelectorAll("#modeSeg .seg"))
    seg.addEventListener("click", () => {
      applyAppearance(currentTheme(), seg.dataset.mode, true);
      populateCustomFields();
    });
  $("customReset").addEventListener("click", resetCustom);
}

function currentTheme() { return document.documentElement.dataset.theme || "claude"; }
function currentMode() { return document.documentElement.dataset.mode === "dark" ? "dark" : "light"; }

function updateModeSeg() {
  const mode = currentMode();
  for (const seg of document.querySelectorAll("#modeSeg .seg"))
    seg.classList.toggle("active", seg.dataset.mode === mode);
}

function applyAppearance(theme, mode, persist) {
  const root = document.documentElement;
  root.dataset.theme = theme;
  root.dataset.mode = mode;
  applyOverrides(theme, mode);
  updateModeSeg();
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

// ---- Manual colour overrides ----------------------------------------------
function loadCustom() {
  try { return JSON.parse(localStorage.getItem("hc-custom") || "{}"); }
  catch { return {}; }
}
function saveCustom(obj) {
  try { localStorage.setItem("hc-custom", JSON.stringify(obj)); } catch { /* ignore */ }
}
function customKey() { return currentTheme() + ":" + currentMode(); }

// Clear any inline overrides, then apply the saved set for this theme+mode.
function applyOverrides(theme, mode) {
  const root = document.documentElement;
  for (const t of CUSTOM_TOKENS) root.style.removeProperty(t.v);
  const over = loadCustom()[theme + ":" + mode] || {};
  for (const k of Object.keys(over))
    if (k.indexOf("--") === 0) root.style.setProperty(k, over[k]);
}

function populateCustomFields() {
  const wrap = $("customFields");
  wrap.innerHTML = "";
  const cs = getComputedStyle(document.documentElement);

  for (const tok of CUSTOM_TOKENS) {
    const cur = toHex(cs.getPropertyValue(tok.v).trim());
    const field = document.createElement("label");
    field.className = "custom-field";
    field.innerHTML = `<input type="color" value="${cur}" /><span></span>`;
    field.querySelector("span").textContent = tok.label;
    const input = field.querySelector("input");
    input.addEventListener("input", () => setCustomVar(tok.v, input.value));
    wrap.appendChild(field);
  }
}

function setCustomVar(name, value) {
  document.documentElement.style.setProperty(name, value);
  const all = loadCustom();
  const key = customKey();
  all[key] = all[key] || {};
  all[key][name] = value;
  saveCustom(all);
  // Keep the browser chrome colour synced if the background was edited.
  if (name === "--canvas") {
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute("content", value);
  }
}

function resetCustom() {
  const all = loadCustom();
  delete all[customKey()];
  saveCustom(all);
  applyOverrides(currentTheme(), currentMode());
  populateCustomFields(); // repopulate inputs from the base scheme values
}

function agentThemeName(id) {
  return ({ claude: "Claude", github: "GitHub", openai: "OpenAI", opencode: "opencode", obsidian: "Obsidian" })[id] || id;
}

// Parse #hex / rgb() / rgba() into a 6-digit #hex the color input accepts.
function toHex(str) {
  if (!str) return "#888888";
  str = str.trim();
  if (str[0] === "#") {
    if (str.length === 4) // #abc -> #aabbcc
      return "#" + str.slice(1).split("").map((c) => c + c).join("");
    return str.slice(0, 7);
  }
  const m = str.match(/rgba?\(([^)]+)\)/i);
  if (m) {
    const p = m[1].split(",").map((x) => parseFloat(x.trim()));
    const h = (n) => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, "0");
    return "#" + h(p[0]) + h(p[1]) + h(p[2]);
  }
  return "#888888";
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
  // In-private sessions never appear in the list.
  sessions = (sessions || []).filter((s) => !state.privateIds.has(s.id));
  if (!sessions.length) {
    list.innerHTML = state.config.noHistory
      ? `<div class="empty muted">History is off (--no-history).<br/>Start a new session above.</div>`
      : `<div class="empty muted">No sessions yet.<br/>Start a new one above.</div>`;
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
      list.appendChild(group(agentName(prov), items, false, true, "agent:" + prov));
  } else {
    // Recent: flat, newest first (already sorted server-side), show project path.
    for (const s of sessions) list.appendChild(sessionItem(s, true));
  }
}

function group(headText, items, showAgent, collapsible, key) {
  const g = document.createElement("div");
  g.className = "project-group";
  const head = document.createElement("div");
  head.className = "project-head";

  if (collapsible) {
    const gkey = key || headText;
    head.classList.add("collapsible");
    if (state.collapsed.has(gkey)) g.classList.add("collapsed");
    head.innerHTML = `<span class="caret">▾</span><span class="grp-name"></span><span class="grp-count"></span>`;
    head.querySelector(".grp-name").textContent = headText;
    head.querySelector(".grp-count").textContent = items.length;
    head.title = headText;
    head.addEventListener("click", () => toggleCollapsed(gkey, g));
  } else {
    head.textContent = headText;
    head.title = headText;
  }

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
  // Leaving an in-private session that wasn't minimized discards its history.
  const prev = state.current;
  if (prev && prev.private && prev.id &&
      !(sess && sess.id === prev.id && sess.provider === prev.provider) &&
      !state.minimized.some((m) => m.id === prev.id && m.provider === prev.provider)) {
    purgePrivate(prev);
  }
  state.current = sess;
  markActive();
  $("chatTitle").textContent = sess.title || "Session";
  $("chatSub").textContent = `${agentName(sess.provider)} · ${shortenPath(sess.cwd)}`;
  $("cwdChip").textContent = shortenPath(sess.cwd);
  $("composer").hidden = false;
  if (sess.private && sess.id) state.privateIds.add(sess.id);
  updateSessionButtons();
  app.classList.add("show-chat");
  await loadTranscript(sess);
}

// Topbar button visibility:
//   rename / share -> the selected (open) session, once it has an id
//   minimize       -> in-private sessions only
function updateSessionButtons() {
  const s = state.current;
  const hasId = !!(s && s.id);
  $("renameBtn").hidden = !hasId;
  $("shareBtn").hidden = !hasId;
  $("minimizeBtn").hidden = !(s && s.private);
  $("privateBadge").hidden = !(s && s.private);
}

// ---- In-private minimize ---------------------------------------------------
function minimizeCurrent() {
  const s = state.current;
  if (!s) return;
  if (!state.minimized.some((m) => m.id === s.id && m.provider === s.provider))
    state.minimized.push(s);
  renderMinimized();
  clearMainView();
}

function renderMinimized() {
  const bar = $("minimizedBar");
  bar.innerHTML = "";
  for (const m of state.minimized) {
    const pill = document.createElement("div");
    pill.className = "min-pill";
    pill.innerHTML = `<span class="min-dot"></span><span class="min-title"></span><span class="min-close" title="Close">✕</span>`;
    pill.querySelector(".min-title").textContent = m.title || "Private session";
    pill.addEventListener("click", (e) => {
      if (e.target.classList.contains("min-close")) closeMinimized(m);
      else restoreMinimized(m);
    });
    bar.appendChild(pill);
  }
  bar.hidden = state.minimized.length === 0;
}

function restoreMinimized(m) {
  state.minimized = state.minimized.filter((x) => !(x.id === m.id && x.provider === m.provider));
  renderMinimized();
  openSession(m);
}

// Closing a minimized in-private session discards it — and its transcript.
function closeMinimized(m) {
  state.minimized = state.minimized.filter((x) => !(x.id === m.id && x.provider === m.provider));
  renderMinimized();
  purgePrivate(m);
}

// Delete an in-private session's stored transcript so it isn't kept in history.
function purgePrivate(sess) {
  if (!sess || !sess.id) return;
  state.privateIds.delete(sess.id);
  const url = `/api/sessions/${encodeURIComponent(sess.provider || "claude")}/${encodeURIComponent(sess.id)}/purge`;
  try {
    if (navigator.sendBeacon) navigator.sendBeacon(url);
    else fetch(url, { method: "POST", keepalive: true });
  } catch { /* best effort */ }
}

function clearMainView() {
  state.current = null;
  $("composer").hidden = true;
  updateSessionButtons();   // no session -> hide rename/share/minimize + badge
  $("chatTitle").textContent = "Select a session";
  $("chatSub").textContent = "";
  markActive();
  app.classList.remove("show-chat");
  $("transcript").innerHTML =
    `<div class="welcome"><div class="welcome-mark">✳</div>` +
    `<h1>Your thinking partner, headless.</h1>` +
    `<p class="muted">Pick a session on the left, or start a new one.</p></div>`;
}

// ---- Rename session --------------------------------------------------------
function openRename() {
  const s = state.current;
  if (!s || !s.id) return;
  $("renameInput").value = s.title || "";
  $("renameDialog").showModal();
  setTimeout(() => $("renameInput").select(), 50);
}

async function onRenameSubmit(e) {
  const btn = e.submitter;
  if (!btn || btn.value !== "ok") return;
  const s = state.current;
  if (!s || !s.id) return;
  const title = $("renameInput").value.trim();
  try {
    await fetch(`/api/sessions/${encodeURIComponent(s.id)}/rename`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title }),
    });
    if (title) { s.title = title; $("chatTitle").textContent = title; }
    await loadSessions();  // refresh sidebar; also recovers the original title when cleared
    const fresh = state.sessions.find((x) => x.id === s.id && (x.provider || "claude") === s.provider);
    if (fresh) { s.title = fresh.title; $("chatTitle").textContent = fresh.title; }
    showToast(title ? "Renamed" : "Title reset");
  } catch { showToast("Rename failed"); }
}

// ---- Share links -----------------------------------------------------------
function shareCurrent() {
  const s = state.current;
  if (!s || !s.id) return;
  const link = `${location.origin}/?s=${encodeURIComponent(s.provider + ":" + s.project + ":" + s.id)}`;
  copyText(link).then(
    () => showToast("Share link copied to clipboard"),
    () => showToast(link)
  );
}

function applyDeepLink() {
  const raw = new URLSearchParams(location.search).get("s");
  if (!raw) return;
  // Clean the URL so a refresh doesn't keep reopening.
  history.replaceState(null, "", "/");
  const [provider, project, id] = raw.split(":");
  if (!provider || !id) return;
  const found = state.sessions.find((x) => x.id === id && (x.provider || "claude") === provider);
  if (found)
    openSession({ provider, project: found.projectId, id, cwd: found.cwd, title: found.title });
  else
    openSession({ provider, project, id, cwd: "", title: "Shared session" });
}

function copyText(text) {
  if (navigator.clipboard && navigator.clipboard.writeText)
    return navigator.clipboard.writeText(text);
  return new Promise((resolve, reject) => {
    try {
      const ta = document.createElement("textarea");
      ta.value = text; ta.style.position = "fixed"; ta.style.opacity = "0";
      document.body.appendChild(ta); ta.select();
      document.execCommand("copy"); document.body.removeChild(ta);
      resolve();
    } catch (e) { reject(e); }
  });
}

let toastTimer = null;
function showToast(msg) {
  const t = $("toast");
  t.textContent = msg;
  t.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { t.hidden = true; }, 2600);
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
  if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); return; }
  if (e.key === "Tab" && e.shiftKey) { e.preventDefault(); cyclePermMode(); }
}

// Shift+Tab cycles the working mode (Ask → Accept edits → Plan → Bypass → …).
function cyclePermMode() {
  const sel = $("permMode");
  sel.selectedIndex = (sel.selectedIndex + 1) % sel.options.length;
  showToast("Mode: " + sel.options[sel.selectedIndex].textContent);
}
function autoGrow() {
  const el = $("input");
  const expanded = $("composer").classList.contains("expanded");
  const max = expanded ? Math.round(window.innerHeight * 0.5) : 200;
  el.style.height = "auto";
  el.style.height = Math.min(el.scrollHeight, max) + "px";
}

// Toggle the composer between the compact one-line box and a taller ~5-line box.
function toggleExpand() {
  const c = $("composer");
  const expanded = c.classList.toggle("expanded");
  const btn = $("expandBtn");
  btn.textContent = expanded ? "⤡" : "⤢";
  btn.title = expanded ? "Shrink input" : "Expand input (5 lines)";
  autoGrow();
  $("input").focus();
}

// ---- Terminal (only available when server started with --commands-allowed) --
function toggleTerminal() {
  const w = $("terminalWindow");
  if (w.hidden) {
    w.hidden = false;
    w.classList.remove("minimized");
    $("terminalInput").focus();
  } else {
    w.hidden = true;
  }
}
function minimizeTerminal() { $("terminalWindow").classList.add("minimized"); }
function restoreTerminal() {
  const w = $("terminalWindow");
  if (w.classList.contains("minimized")) {
    w.classList.remove("minimized");
    $("terminalInput").focus();
  }
}
function termAppend(text, cls) {
  const out = $("terminalOutput");
  const line = document.createElement("div");
  if (cls) line.className = cls;
  line.textContent = text;
  out.appendChild(line);
  out.scrollTop = out.scrollHeight;
}
async function runTerminalCommand(e) {
  if (e) e.preventDefault();
  if (state.termBusy) return;
  const input = $("terminalInput");
  const cmd = input.value.trim();
  if (!cmd) return;
  input.value = "";
  termAppend("$ " + cmd, "t-cmd");
  state.termBusy = true;
  try {
    const resp = await fetch("/api/terminal", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ command: cmd, cwd: (state.current && state.current.cwd) || "" }),
    });
    if (resp.status === 403) { termAppend("Terminal is disabled on the server.", "t-err"); return; }
    if (!resp.ok || !resp.body) { termAppend("HTTP " + resp.status, "t-err"); return; }
    const reader = resp.body.getReader();
    const decoder = new TextDecoder();
    let buf = "";
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buf += decoder.decode(value, { stream: true });
      let idx;
      while ((idx = buf.indexOf("\n\n")) >= 0) {
        handleTermSse(buf.slice(0, idx));
        buf = buf.slice(idx + 2);
      }
    }
  } catch (err) {
    termAppend(String(err && err.message ? err.message : err), "t-err");
  } finally {
    state.termBusy = false;
    $("terminalInput").focus();
  }
}
function handleTermSse(frame) {
  let event = "line";
  const dataLines = [];
  for (const line of frame.split("\n")) {
    if (line.startsWith("event:")) event = line.slice(6).trim();
    else if (line.startsWith("data:")) dataLines.push(line.slice(5).replace(/^ /, ""));
  }
  if (event !== "line") return;
  let obj;
  try { obj = JSON.parse(dataLines.join("\n")); } catch { return; }
  if (obj.kind === "exit") termAppend(`[exit ${obj.text}]`, "t-exit");
  else termAppend(obj.text, obj.kind === "stderr" ? "t-err" : null);
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
    effort: $("effort").value || null,
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
        if (state.current.private) state.privateIds.add(meta.sessionId);
        updateSessionButtons();   // reveal rename/share once the new session has an id
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
  const body = document.createElement("div");
  body.className = "md";
  body.innerHTML = renderMarkdown(text);   // format the user's text (e.g. "* " → bullet list)
  d.appendChild(body);
  $("transcript").appendChild(d);
}
function addAssistantBubble(text) {
  const wrap = document.createElement("div");
  wrap.className = "msg msg-assistant";
  wrap.innerHTML = `<div class="msg-role"></div>`;
  wrap.querySelector(".msg-role").textContent = state.current ? agentName(state.current.provider) : "Assistant";
  const body = document.createElement("div");
  body.className = "md";
  body.innerHTML = renderMarkdown(text);
  wrap.appendChild(body);
  $("transcript").appendChild(wrap);
}
function addToolChip(name, detail) {
  const isResult = name === "result";
  const d = document.createElement("div");
  d.className = "tool" + (isResult ? " tool-result" : "");

  let body = (detail || "").trim();
  let summary = "";
  if (body) {
    try {
      const obj = JSON.parse(body);
      if (obj && typeof obj === "object") {
        body = JSON.stringify(obj, null, 2);            // pretty-print structured input
        summary = toolSummary(obj) || firstLine(body);
      } else {
        summary = firstLine(body);
      }
    } catch {
      summary = firstLine(body);                        // plain text (e.g. command output)
    }
  }
  const hasBody = body.length > 0;

  const head = document.createElement("button");
  head.type = "button";
  head.className = "tool-head";
  head.innerHTML =
    `<span class="tool-icon">${isResult ? "↳" : "🔧"}</span>` +
    `<span class="tool-name"></span>` +
    `<span class="tool-summary"></span>` +
    (hasBody ? `<span class="tool-caret">▸</span>` : "");
  head.querySelector(".tool-name").textContent = name;
  head.querySelector(".tool-summary").textContent = summary;
  d.appendChild(head);

  if (hasBody) {
    const pre = document.createElement("pre");
    pre.className = "tool-body";
    pre.textContent = body;
    d.appendChild(pre);
    head.addEventListener("click", () => d.classList.toggle("open"));
  } else {
    head.classList.add("no-body");
  }
  $("transcript").appendChild(d);
}

// A short one-line summary of a tool call's input (e.g. the command / file / url).
function toolSummary(obj) {
  const f = obj.command || obj.file_path || obj.path || obj.pattern ||
            obj.url || obj.query || obj.description || obj.prompt || obj.question;
  if (typeof f !== "string") return "";
  return clip(f.replace(/\s+/g, " ").trim());
}
function firstLine(s) { return clip(String(s).split("\n")[0].trim()); }
function clip(s) { return s.length > 100 ? s.slice(0, 100) + "…" : s; }
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
    // Replace the plain streamed text with rendered Markdown.
    state.liveEl.classList.add("md");
    state.liveEl.innerHTML = renderMarkdown(text);
    state.liveEl.parentElement.classList.remove("live");
    state.liveEl = null;
  } else {
    addAssistantBubble(text);
  }
}

// ---- New session dialog ----------------------------------------------------
function openNewDialog() {
  renderAgentPicker();
  $("newPrivate").checked = false;
  const freeStyle = !!state.config.freeStyle;
  const input = $("newCwd");
  const select = $("newCwdSelect");
  const preferred = state.current ? state.current.cwd : (state.projects[0]?.path || "");

  if (freeStyle) {
    // Any folder: free-text input with project suggestions.
    input.hidden = false;
    select.hidden = true;
    input.value = preferred;
    $("newCwdHint").textContent = "Type any folder path on the host machine, or pick a project.";
  } else {
    // Restricted: a dropdown of existing project directories.
    input.hidden = true;
    select.hidden = false;
    select.innerHTML = "";
    for (const p of state.projects) {
      const o = document.createElement("option");
      o.value = p.path; o.textContent = p.path;
      select.appendChild(o);
    }
    if (!state.projects.length) {
      const o = document.createElement("option");
      o.value = ""; o.textContent = "No existing projects — start with --free-style";
      o.disabled = true;
      select.appendChild(o);
    }
    if (state.projects.some((p) => p.path === preferred)) select.value = preferred;
    $("newCwdHint").textContent = "Pick one of your existing projects (start with --free-style for any folder).";
  }

  if (state.current && state.current.provider) $("newAgent").value = state.current.provider;
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
  const freeStyle = !!state.config.freeStyle;
  const cwd = (freeStyle ? $("newCwd").value : $("newCwdSelect").value).trim();
  const provider = $("newAgent").value || "claude";
  const isPrivate = $("newPrivate").checked;
  if (!cwd) { e.preventDefault(); return; }
  openSession({ provider, project: null, id: null, cwd, title: isPrivate ? "Private session" : "New session", private: isPrivate });
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

// ---- Minimal, safe Markdown renderer --------------------------------------
// Everything is HTML-escaped before any markup is added, so agent output cannot
// inject HTML. Supports headings, lists, code (inline + fenced), blockquotes,
// tables, bold/italic/strike, links and horizontal rules.
function renderMarkdown(src) {
  if (!src) return "";
  src = String(src).replace(/\r\n?/g, "\n");

  // Pull fenced code blocks out first (escaped, protected from inline rules).
  const blocks = [];
  src = src.replace(/```[^\n`]*\n([\s\S]*?)```/g, (_m, code) => {
    blocks.push(`<pre><code>${escapeHtml(code.replace(/\n$/, ""))}</code></pre>`);
    return ` CB${blocks.length - 1} `;
  });

  const lines = src.split("\n");
  let html = "", i = 0;

  while (i < lines.length) {
    const line = lines[i];
    let m;

    if ((m = line.match(/^ CB(\d+) $/))) { html += blocks[+m[1]]; i++; continue; }
    if (/^\s*$/.test(line)) { i++; continue; }
    if (/^\s*([-*_])(\s*\1){2,}\s*$/.test(line)) { html += "<hr/>"; i++; continue; }
    if ((m = line.match(/^(#{1,4})\s+(.*)$/))) { html += `<h${m[1].length}>${mdInline(m[2].trim())}</h${m[1].length}>`; i++; continue; }

    if (/^\s*>\s?/.test(line)) {
      const buf = [];
      while (i < lines.length && /^\s*>\s?/.test(lines[i])) { buf.push(lines[i].replace(/^\s*>\s?/, "")); i++; }
      html += `<blockquote>${renderMarkdown(buf.join("\n"))}</blockquote>`;
      continue;
    }

    if (/^\s*([-*+]|\d+\.)\s+/.test(line)) {
      const ordered = /^\s*\d+\.\s+/.test(line);
      const tag = ordered ? "ol" : "ul";
      let out = `<${tag}>`;
      while (i < lines.length && /^\s*([-*+]|\d+\.)\s+/.test(lines[i])) {
        out += `<li>${mdInline(lines[i].replace(/^\s*([-*+]|\d+\.)\s+/, ""))}</li>`;
        i++;
      }
      html += out + `</${tag}>`;
      continue;
    }

    // Tables: header row followed by a |---|---| separator.
    if (line.indexOf("|") >= 0 && i + 1 < lines.length && /^\s*\|?[\s:-]*-[\s:|-]*$/.test(lines[i + 1]) && lines[i + 1].indexOf("|") >= 0) {
      const row = (r) => r.replace(/^\s*\|/, "").replace(/\|\s*$/, "").split("|").map((c) => c.trim());
      const headers = row(line);
      i += 2;
      let out = `<table><thead><tr>${headers.map((h) => `<th>${mdInline(h)}</th>`).join("")}</tr></thead><tbody>`;
      while (i < lines.length && lines[i].indexOf("|") >= 0 && !/^\s*$/.test(lines[i])) {
        out += `<tr>${row(lines[i]).map((c) => `<td>${mdInline(c)}</td>`).join("")}</tr>`;
        i++;
      }
      html += out + "</tbody></table>";
      continue;
    }

    // Paragraph.
    const para = [];
    while (i < lines.length && !/^\s*$/.test(lines[i]) &&
      !/^(#{1,4})\s/.test(lines[i]) && !/^\s*>/.test(lines[i]) &&
      !/^\s*([-*+]|\d+\.)\s+/.test(lines[i]) && !/^ CB\d+ $/.test(lines[i]) &&
      !/^\s*([-*_])(\s*\1){2,}\s*$/.test(lines[i])) {
      para.push(lines[i]); i++;
    }
    html += `<p>${mdInline(para.join("\n")).replace(/\n/g, "<br/>")}</p>`;
  }

  return html.replace(/ CB(\d+) /g, (_m, n) => blocks[+n] || "");
}

function mdInline(t) {
  t = escapeHtml(t);
  const codes = [];
  t = t.replace(/`([^`]+)`/g, (_m, c) => { codes.push(c); return `${codes.length - 1}`; });
  t = t.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_m, txt, url) => {
    const safe = /^(https?:|mailto:|\/)/i.test(url) ? url : "#";
    return `<a href="${safe}" target="_blank" rel="noopener noreferrer">${txt}</a>`;
  });
  t = t.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  t = t.replace(/__([^_]+)__/g, "<strong>$1</strong>");
  t = t.replace(/(^|[^*])\*([^*\n]+)\*/g, "$1<em>$2</em>");
  t = t.replace(/~~([^~]+)~~/g, "<del>$1</del>");
  t = t.replace(/(\d+)/g, (_m, n) => `<code>${codes[+n]}</code>`);
  return t;
}
