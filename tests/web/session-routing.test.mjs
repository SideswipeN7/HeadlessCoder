import { test } from "node:test";
import assert from "node:assert/strict";
import { loadApp } from "./harness.mjs";

// Regression tests for the cross-session SSE leak: a stream belonging to one session
// must never render into whatever session the user has switched to, yet must still
// record its own session id in the background so a later resume works.

test("app.js loads and exposes the streaming functions", () => {
  const window = loadApp();
  assert.equal(typeof window.dispatchAgentEvent, "function");
  assert.equal(typeof window.handleSse, "function");
  assert.equal(window.eval("typeof state"), "object");
});

test("a background session's agent events do not leak into the active view", () => {
  const window = loadApp();
  const r = window.eval(`(() => {
    const t = document.getElementById("transcript");
    const A = { provider: "claude", project: "p", id: "A-id", cwd: "/a", title: "A" };
    const B = { provider: "claude", project: "p", id: "B-id", cwd: "/b", title: "B" };

    state.current = A; state.liveEl = null; t.innerHTML = "";
    const before = t.children.length;

    // Session B streams in the background while we are viewing Session A.
    dispatchAgentEvent({ kind: "system", sessionId: "B-new" }, B);
    dispatchAgentEvent({ kind: "tool", toolName: "Read", text: "SECRET_FROM_B" }, B);
    dispatchAgentEvent({ kind: "text_delta", text: "leak-from-B" }, B);
    dispatchAgentEvent({ kind: "result", durationMs: 5 }, B);

    const leaked = t.textContent.includes("SECRET_FROM_B") || t.textContent.includes("leak-from-B");
    const bgChildCount = t.children.length;

    // Session A (the active view) streams — this must render normally.
    dispatchAgentEvent({ kind: "tool", toolName: "Read", text: "VISIBLE_IN_A" }, A);

    return {
      leaked,
      unchangedDuringBackground: bgChildCount === before,
      backgroundIdUpdated: B.id === "B-new",
      activeIdUntouched: A.id === "A-id",
      activeRendered: t.textContent.includes("VISIBLE_IN_A"),
    };
  })()`);

  assert.equal(r.leaked, false, "background stream leaked into the active view");
  assert.equal(r.unchangedDuringBackground, true, "background stream mutated the active transcript");
  assert.equal(r.backgroundIdUpdated, true, "background session id was not recorded for resume");
  assert.equal(r.activeIdUntouched, true, "active session id was corrupted by the background stream");
  assert.equal(r.activeRendered, true, "active session stream failed to render");
});

test("handleSse routes meta + multi-line agent frames to the owning session only", () => {
  const window = loadApp();
  const r = window.eval(`(() => {
    const t = document.getElementById("transcript");
    const A = { provider: "claude", project: "p", id: null, cwd: "/a", title: "A", private: false };
    const B = { provider: "claude", project: "p", id: null, cwd: "/b", title: "B", private: false };
    state.current = A; state.liveEl = null; t.innerHTML = "";

    // Brand-new background session B is assigned its id via a meta frame.
    handleSse('event: meta\\ndata: {"sessionId":"B-123","isNew":true,"provider":"claude"}', B);

    // Multi-line (pretty-printed) agent frame for background B must not render into A.
    handleSse('event: agent\\ndata: {\\ndata: "kind": "assistant",\\ndata: "text": "B answer"\\ndata: }', B);

    const bgLeaked = t.textContent.includes("B answer");
    const bgId = B.id, activeId = A.id;

    // Same-shaped frame for the active session A must render.
    handleSse('event: agent\\ndata: {\\ndata: "kind": "assistant",\\ndata: "text": "A answer"\\ndata: }', A);

    return {
      backgroundMetaSetOwnerId: bgId === "B-123",
      activeIdUntouched: activeId === null,
      backgroundNotRendered: bgLeaked === false,
      activeRendered: t.textContent.includes("A answer"),
    };
  })()`);

  assert.equal(r.backgroundMetaSetOwnerId, true, "meta frame did not set the background owner's id");
  assert.equal(r.activeIdUntouched, true, "meta frame corrupted the active session's id");
  assert.equal(r.backgroundNotRendered, true, "background agent frame leaked into the active view");
  assert.equal(r.activeRendered, true, "active agent frame failed to render");
});

test("switching the active view resets the stale live-bubble pointer", () => {
  const window = loadApp();
  const r = window.eval(`(() => {
    const t = document.getElementById("transcript");
    const A = { provider: "claude", project: "p", id: "A", cwd: "/a", title: "A" };
    state.current = A; t.innerHTML = "";

    // Start a live assistant bubble for A.
    dispatchAgentEvent({ kind: "text_delta", text: "partial" }, A);
    const hadLive = !!state.liveEl;

    // Switching views (clearMainView) must drop the stale live pointer so a later
    // stream cannot resume into a detached node.
    clearMainView();

    return { hadLive, clearedLive: state.liveEl === null, clearedCurrent: state.current === null };
  })()`);

  assert.equal(r.hadLive, true, "expected a live bubble to exist mid-stream");
  assert.equal(r.clearedLive, true, "live-bubble pointer was not reset on view switch");
  assert.equal(r.clearedCurrent, true, "current session was not cleared");
});
