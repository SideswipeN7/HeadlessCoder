"use strict";

// ---- State -----------------------------------------------------------------
const state = {
  current: null,      // { project, id, cwd, title } | null (new = id null)
  sending: false,
  liveEl: null,       // in-progress assistant bubble
  projects: [],
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

  loadHealth();
  loadSessions();
});

// ---- Health / status -------------------------------------------------------
async function loadHealth() {
  try {
    const h = await (await fetch("/api/health")).json();
    $("statusFoot").textContent = h.storeAvailable
      ? `claude: ${basename(h.claude)} · ready`
      : "⚠ ~/.claude/projects not found";
  } catch {
    $("statusFoot").textContent = "⚠ backend unreachable";
  }
}

// ---- Session list ----------------------------------------------------------
async function loadSessions() {
  const list = $("sessionList");
  try {
    const [sessions, projects] = await Promise.all([
      fetch("/api/sessions").then((r) => r.json()),
      fetch("/api/projects").then((r) => r.json()),
    ]);
    state.projects = projects;
    renderProjectDatalist(projects);

    if (!sessions.length) {
      list.innerHTML = `<div class="empty muted">No sessions yet.<br/>Start a new one above.</div>`;
      return;
    }

    // Group sessions by project path.
    const groups = new Map();
    for (const s of sessions) {
      if (!groups.has(s.cwd)) groups.set(s.cwd, []);
      groups.get(s.cwd).push(s);
    }

    list.innerHTML = "";
    for (const [cwd, items] of groups) {
      const g = document.createElement("div");
      g.className = "project-group";
      const head = document.createElement("div");
      head.className = "project-head";
      head.textContent = shortenPath(cwd);
      head.title = cwd;
      g.appendChild(head);

      for (const s of items) g.appendChild(sessionItem(s));
      list.appendChild(g);
    }
  } catch (e) {
    list.innerHTML = `<div class="empty muted">Failed to load sessions.</div>`;
  }
}

function sessionItem(s) {
  const btn = document.createElement("button");
  btn.className = "session-item";
  btn.dataset.id = s.id;
  if (state.current && state.current.id === s.id) btn.classList.add("active");

  const meta = [];
  if (s.messageCount) meta.push(`${s.messageCount} msgs`);
  if (s.lastActivity) meta.push(relTime(s.lastActivity));

  btn.innerHTML = `
    <div class="s-title"></div>
    <div class="s-meta">
      ${s.gitBranch ? `<span class="branch-pill"></span>` : ""}
      <span class="s-metatext"></span>
    </div>`;
  btn.querySelector(".s-title").textContent = s.title;
  btn.querySelector(".s-metatext").textContent = meta.join(" · ");
  if (s.gitBranch) btn.querySelector(".branch-pill").textContent = s.gitBranch;

  btn.addEventListener("click", () =>
    openSession({ project: s.projectId, id: s.id, cwd: s.cwd, title: s.title }));
  return btn;
}

// ---- Open / render a session ----------------------------------------------
async function openSession(sess) {
  state.current = sess;
  markActive();
  $("chatTitle").textContent = sess.title || "Session";
  $("chatSub").textContent = shortenPath(sess.cwd);
  $("cwdChip").textContent = shortenPath(sess.cwd);
  $("composer").hidden = false;
  app.classList.add("show-chat");
  await loadTranscript(sess);
}

async function loadTranscript(sess) {
  const t = $("transcript");
  t.innerHTML = `<div class="empty muted">Loading transcript…</div>`;
  try {
    const msgs = sess.id
      ? await fetch(`/api/sessions/${encodeURIComponent(sess.project)}/${encodeURIComponent(sess.id)}`).then((r) => r.json())
      : [];
    t.innerHTML = "";
    if (!msgs.length) {
      addWelcomeInline();
    } else {
      for (const m of msgs) renderTranscriptMessage(m);
    }
    scrollBottom();
  } catch {
    t.innerHTML = `<div class="empty muted">Failed to load transcript.</div>`;
  }
}

function addWelcomeInline() {
  const d = document.createElement("div");
  d.className = "result-line";
  d.textContent = "New session — send a message to begin.";
  $("transcript").appendChild(d);
}

function renderTranscriptMessage(m) {
  if (m.role === "user") return addUserBubble(m.text);
  if (m.role === "assistant") return addAssistantBubble(m.text);
  if (m.role === "tool") return addToolChip(m.toolName || "tool", m.text);
}

// ---- Composing / sending ---------------------------------------------------
function onInputKey(e) {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    send();
  }
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
    // Refresh sidebar so a brand-new session appears with its real title.
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
      const frame = buf.slice(0, idx);
      buf = buf.slice(idx + 2);
      handleSse(frame);
    }
  }
}

function handleSse(frame) {
  let event = "event";
  const dataLines = [];
  for (const line of frame.split("\n")) {
    if (line.startsWith("event:")) event = line.slice(6).trim();
    else if (line.startsWith("data:")) dataLines.push(line.slice(5).replace(/^ /, ""));
  }
  const data = dataLines.join("\n");

  if (event === "meta") {
    try {
      const meta = JSON.parse(data);
      if (meta.isNew && meta.sessionId) {
        state.current.id = meta.sessionId;
        state.current.project = state.current.project || null;
      }
    } catch { /* ignore */ }
    return;
  }
  if (event === "done") { endLive(); return; }
  if (event === "error") {
    endLive();
    try { addErrorLine(JSON.parse(data).error); } catch { addErrorLine(data); }
    return;
  }

  // event === "event": a raw claude stream-json object
  let obj;
  try { obj = JSON.parse(data); } catch { return; }
  dispatchStreamEvent(obj);
}

function dispatchStreamEvent(obj) {
  switch (obj.type) {
    case "system":
      if (obj.subtype === "init" && obj.session_id && state.current)
        state.current.id = obj.session_id;
      break;

    case "stream_event":
      handlePartial(obj.event);
      break;

    case "assistant": {
      const content = (obj.message && obj.message.content) || [];
      const texts = [];
      const tools = [];
      if (Array.isArray(content)) {
        for (const b of content) {
          if (b.type === "text" && b.text && b.text.trim()) texts.push(b.text);
          else if (b.type === "tool_use") tools.push(b);
        }
      }
      // If we streamed this text live, finalize that bubble instead of duplicating it.
      const finalText = texts.join("\n\n");
      if (finalText) finalizeAssistantText(finalText);
      else endLive();
      for (const t of tools) addToolChip(t.name || "tool", compactJson(t.input));
      scrollBottom();
      break;
    }

    case "user": {
      const content = obj.message && obj.message.content;
      if (Array.isArray(content)) {
        for (const b of content) {
          if (b.type === "tool_result")
            addToolChip("result", extractToolResult(b.content));
        }
      }
      break;
    }

    case "result":
      endLive();
      addResultLine(obj);
      break;

    case "error":
      endLive();
      addErrorLine(obj.error || "unknown error");
      break;
  }
}

// Live token streaming from --include-partial-messages.
function handlePartial(ev) {
  if (!ev) return;
  if (ev.type === "content_block_delta" && ev.delta && ev.delta.type === "text_delta") {
    ensureLive().textContent += ev.delta.text || "";
    scrollBottom();
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
  wrap.innerHTML = `<div class="msg-role">Claude</div>`;
  const body = document.createElement("div");
  body.textContent = text;
  wrap.appendChild(body);
  $("transcript").appendChild(wrap);
}
function addToolChip(name, detail) {
  const d = document.createElement("div");
  d.className = "tool";
  d.innerHTML = `<span class="tool-name">🔧 ${escapeHtml(name)}</span>`;
  if (detail && detail.trim()) {
    const pre = document.createElement("pre");
    pre.textContent = detail;
    d.appendChild(pre);
  }
  $("transcript").appendChild(d);
}
function addResultLine(obj) {
  const parts = [];
  if (typeof obj.duration_ms === "number") parts.push(`${(obj.duration_ms / 1000).toFixed(1)}s`);
  if (typeof obj.total_cost_usd === "number") parts.push(`$${obj.total_cost_usd.toFixed(4)}`);
  if (obj.num_turns) parts.push(`${obj.num_turns} turns`);
  if (obj.is_error) parts.push("error");
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
  wrap.innerHTML = `<div class="msg-role">Claude</div>`;
  const body = document.createElement("div");
  wrap.appendChild(body);
  $("transcript").appendChild(wrap);
  state.liveEl = body;
  return body;
}
function endLive() {
  if (state.liveEl) {
    // Drop an empty in-progress bubble (e.g. thinking-only turn) entirely.
    if (!state.liveEl.textContent.trim()) state.liveEl.parentElement.remove();
    else state.liveEl.parentElement.classList.remove("live");
    state.liveEl = null;
  }
}

// Reuse the live-streamed bubble as the authoritative final text; otherwise add one.
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
  $("newCwd").value = state.current ? state.current.cwd : (state.projects[0]?.path || "");
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
  if (!btn || btn.value !== "ok") return;      // cancel
  const cwd = $("newCwd").value.trim();
  if (!cwd) { e.preventDefault(); return; }
  openSession({ project: null, id: null, cwd, title: "New session" });
  $("transcript").innerHTML = "";
  addWelcomeInline();
  setTimeout(() => $("input").focus(), 50);
}

// ---- Small helpers ---------------------------------------------------------
function markActive() {
  for (const el of document.querySelectorAll(".session-item"))
    el.classList.toggle("active", state.current && el.dataset.id === state.current.id);
}
function setSending(on) {
  $("sendBtn").disabled = on;
  $("sendBtn").textContent = on ? "…" : "Send";
}
function scrollBottom() {
  const t = $("transcript");
  t.scrollTop = t.scrollHeight;
}
function compactJson(v) {
  if (v == null) return "";
  try { const s = JSON.stringify(v); return s.length > 600 ? s.slice(0, 600) + "…" : s; }
  catch { return String(v); }
}
function extractToolResult(content) {
  if (typeof content === "string") return content;
  if (Array.isArray(content))
    return content.map((c) => (typeof c === "string" ? c : c.text || "")).join("\n");
  return compactJson(content);
}
function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
function basename(p) { return String(p || "").split(/[\\/]/).pop() || p; }
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
  const d = Math.floor(h / 24);
  return `${d}d ago`;
}
