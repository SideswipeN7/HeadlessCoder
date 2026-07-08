---
version: alpha
name: Obsidian
description: "A knowledge management tool with a deep, customizable dark canvas (#1E1E2E base for popular themes, or the native #202020) built around Obsidian's signature purple accent (#7C3AED / #9580FF) that appears on links, graph nodes, and active states. The interface is unapologetically power-user — a file tree sidebar, tabbed editor panes, a command palette, and a graph view that renders your entire knowledge base as a spatial network. Typography uses the system default stack at clean sizes, with monospace for markdown source. The overall feel is a productive text editor crossed with a local database — private, offline-first, yours."

colors:
  primary: "#7C3AED"
  on-primary: "#ffffff"
  primary-hover: "#6D28D9"
  link: "#9580FF"
  link-hover: "#B4A0FF"
  ink: "#DCDCdc"
  ink-muted: "#888888"
  canvas: "#202020"
  surface-1: "#2A2A2A"
  surface-2: "#303030"
  border: "#3D3D3D"
  graph-node: "#7C3AED"
  graph-edge: "#444444"
  tag: "#7C3AED"
  callout-bg: "#1E1E2E"

typography:
  display:
    fontFamily: "-apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: 32px
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: -0.01em
  body:
    fontFamily: "-apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.7
    letterSpacing: 0
  code:
    fontFamily: "Fira Code, JetBrains Mono, Consolas, monospace"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.5

spacing:
  base: 8px
  scale: [4, 8, 12, 16, 24, 32, 48, 64]

radius:
  sm: 4px
  md: 8px
  lg: 12px
  pill: 9999px

shadows:
  card: "0 2px 8px rgba(0,0,0,0.4)"
  elevated: "0 4px 16px rgba(0,0,0,0.5)"
  modal: "0 8px 32px rgba(0,0,0,0.6)"

motion:
  duration-fast: 80ms
  duration-base: 150ms
  easing: cubic-bezier(0.4, 0, 0.2, 1)
---

## Rationale

**Dark-only default as a philosophical stance** — Offering a dark-first interface when most productivity tools default to light is a deliberate cultural signal to Obsidian's target audience: serious knowledge workers who spend long hours writing and thinking. Dark mode reduces eye strain in extended sessions and communicates that Obsidian is for deep work, not quick tasks.

**Purple links as the product's core visual metaphor** — #9580FF for [[WikiLinks]] makes connected notes visually distinct from standard text at a glance — which is critical because link density is how Obsidian users measure the quality of their knowledge graph. Purple links aren't brand decoration; they're the visualization of intellectual connection.

**Graph view as the product's emotional peak** — The force-directed knowledge graph is Obsidian's signature feature and its most powerful retention mechanism. Every design decision in the graph (purple active nodes, gray edges, red orphan nodes) serves the moment when a user sees their entire knowledge base as a living network for the first time — a genuinely transformative experience.

**System fonts for maximum native feel** — Using -apple-system/Segoe UI rather than a custom typeface is unusual for a product with strong visual opinions, but reflects Obsidian's core value of locality and ownership. Users reading their own notes in their own vault should feel like they're working in their operating system, not inside software.

**CSS variable extensibility as community strategy** — Building the visual system on overridable CSS variables rather than locked-down components serves Obsidian's community-first model: the plugin and theme ecosystem is a retention and growth mechanism. The design system's extensibility is a business decision as much as a technical one.

## 1. Visual Theme & Atmosphere
Obsidian is where knowledge workers build their second brain, and the product design reflects that seriousness. The dark canvas is the default and preferred context — notes read better against dark in long sessions. The graph view is Obsidian's signature visual: a force-directed network of every note and link in your vault, zoomable and explorable. Purple links and nodes are the chromatic anchor of an otherwise monochromatic system. Everything is local, everything is Markdown, and the interface honors that simplicity.

## 2. Color System
- **Canvas**: #202020 — native default dark, deeper than most editor themes
- **Purple accent**: #7C3AED — used for [[WikiLinks]], active graph nodes, selected states
- **Link color**: #9580FF — a lighter purple for inline note links — the most important interactive element
- **Surface layers**: #2A2A2A / #303030 — sidebars, modals, command palette
- **Ink**: #DCDCDC — not pure white — softer on the eyes for long reading/writing sessions
- **Graph colors**: Purple nodes (linked), gray edges, red for orphan notes (no links)

## 3. Typography
System fonts for maximum native feel — Obsidian respects that users are in it for hours and native fonts reduce visual friction. Notes render at generous 16px/1.7 line-height for comfortable reading. Headings use size contrast (H1-H6) with weight boost. Code blocks use monospace (Fira Code if installed). Themes can override all of this — Obsidian's CSS variable system is extensible.

## 4. Components & Patterns
- **File explorer**: Left sidebar, tree view, folder collapse, note count badges
- **Editor tabs**: Top bar with open files as tabs, split pane support
- **Graph view**: Canvas-based force layout, zoom/pan, filter controls, node hover preview
- **Command palette**: Full-screen fuzzy-search modal (Cmd+P) — primary navigation method
- **Backlinks panel**: Right sidebar showing all notes that link to the current note
- **Callout blocks**: Colored notification boxes within notes, custom icon + title

## 5. Spacing & Layout
Sidebar: 260px default, resizable. Editor: content max-width 700px centered in pane. Graph: full window canvas. Dense, information-rich layout. Tab height: 32px. File tree row height: 28px.

## 6. Motion & Interaction
Graph view: nodes repel/attract with physics simulation. Command palette: instant filtering as you type. File open: immediate. Link hover: preview popover slides in. Near-instant throughout — designed for keyboard-first power users.

## Accessibility

### Contrast Ratios
- **Primary on background** (#7C3AED on #202020): 2.9:1 — fails AA (decorative / large-text only; not suitable for body text)
- **Text on background** (#DCDCDC on #202020): 11.9:1 — passes AA, passes AAA
- **Muted on background** (#888888 on #202020): 4.6:1 — passes AA, fails AAA

### Minimum Requirements
- **Touch target**: 44×44px minimum for all interactive elements
- **Focus indicator**: #9580FF outline, 2px, 2px offset
- **Focus contrast**: 5.3:1 against #202020 background

### Motion
- Respects `prefers-reduced-motion`: yes — all transitions and animations should be suppressed
- All transitions use `@media (prefers-reduced-motion: reduce)` guard

### Notes
- The purple primary (#7C3AED) achieves only 2.9:1 on the dark canvas — it must not be used as the sole text color for normal-weight body text; restrict to large text (18px+), icons, or decorative graph nodes
- The link color (#9580FF) is safer at 5.3:1 and passes AA for body links, but falls short of AAA; use it for interactive text only, never for body copy that needs maximum legibility
- #888888 muted text sits right at the AA threshold (4.6:1); do not dim further or pair with surfaces lighter than #202020 without rechecking
- Graph edges (#444444) and border (#3D3D3D) are purely decorative at their low contrast ratios — ensure no text or icon label relies on them for readability
