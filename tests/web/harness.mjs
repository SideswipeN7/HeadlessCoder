import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { JSDOM, VirtualConsole } from "jsdom";

const here = dirname(fileURLToPath(import.meta.url));
const webDir = resolve(here, "../../src/HeadlessCoder/Web");

/**
 * Loads the real embedded UI (index.html + app.js) into a jsdom window and returns it.
 *
 * The app's boot runs on DOMContentLoaded, which has already fired by the time we inject
 * app.js — so no network calls happen and `state` + every function are defined but idle.
 * That lets us drive the actual client code (dispatchAgentEvent / handleSse / state) exactly
 * as the browser would, without a server or a live agent.
 */
export function loadApp() {
  const html = readFileSync(resolve(webDir, "index.html"), "utf8");
  const appJs = readFileSync(resolve(webDir, "app.js"), "utf8");

  const virtualConsole = new VirtualConsole(); // swallow jsdom "not implemented" noise
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    pretendToBeVisual: true,
    url: "http://localhost/",
    virtualConsole,
  });
  const { window } = dom;

  // Network must never be hit from these tests; fail loudly if the app ever tries.
  window.fetch = () => Promise.reject(new Error("fetch is not available in web unit tests"));
  if (!window.matchMedia)
    window.matchMedia = () => ({ matches: false, addEventListener() {}, removeEventListener() {} });

  const script = window.document.createElement("script");
  script.textContent = appJs;
  window.document.body.appendChild(script); // executes synchronously under runScripts: dangerously

  return window;
}
