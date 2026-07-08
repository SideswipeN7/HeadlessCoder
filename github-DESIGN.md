---
version: alpha
name: GitHub
description: "A developer-native canvas built around the Primer design system — near-black (#0d1117) in dark mode, white (#ffffff) in light — with GitHub's signature accent blue (#0969DA) for interactive states and a green (#1a7f37) used meaningfully for open issues and successful builds. The system reads as technical documentation made beautiful: monospace code blocks, tight information density, and zero decorative chrome. Mona Sans, GitHub's custom variable font, brings humanity to the heading hierarchy while preserving the utilitarian character of a tool built by engineers for engineers."

colors:
  primary: "#0969DA"
  on-primary: "#ffffff"
  primary-hover: "#0860CA"
  secondary: "#1a7f37"
  ink: "#1F2328"
  ink-muted: "#636C76"
  canvas: "#ffffff"
  surface-1: "#F6F8FA"
  surface-2: "#EAEEF2"
  border: "#D0D7DE"
  canvas-dark: "#0d1117"
  surface-dark-1: "#161B22"
  surface-dark-2: "#21262D"
  border-dark: "#30363D"
  ink-dark: "#E6EDF3"
  ink-muted-dark: "#8B949E"
  success: "#1a7f37"
  danger: "#CF222E"
  warning: "#9A6700"
  open: "#1a7f37"
  closed: "#CF222E"
  merged: "#8250DF"
  code-bg: "#F6F8FA"

typography:
  display:
    fontFamily: "Mona Sans, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif"
    fontSize: 48px
    fontWeight: 800
    lineHeight: 1.1
    letterSpacing: -0.5px
  body:
    fontFamily: "-apple-system, BlinkMacSystemFont, Segoe UI, Noto Sans, Helvetica, Arial, sans-serif"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.5
    letterSpacing: 0
  code:
    fontFamily: "ui-monospace, SFMono-Regular, SF Mono, Menlo, Consolas, monospace"
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.45

spacing:
  base: 8px
  scale: [4, 8, 12, 16, 24, 32, 40, 48, 64, 96]

radius:
  sm: 3px
  md: 6px
  lg: 12px
  pill: 9999px

shadows:
  card: "0 1px 0 rgba(27,31,36,0.04), 0 1px 3px rgba(27,31,36,0.06)"
  elevated: "0 8px 24px rgba(140,149,159,0.2)"
  overlay: "0 16px 32px rgba(1,4,9,0.85)"

motion:
  duration-fast: 80ms
  duration-base: 160ms
  easing: cubic-bezier(0.3, 0, 0.5, 1)
---

## 1. Visual Theme & Atmosphere
GitHub's Primer system is one of the most studied design systems in open source. The interface prioritizes information over aesthetics — repositories, issues, and code are the content; the UI is the transparent container. Dark mode (#0d1117) is the preferred developer context, borrowing from terminal tradition. Light mode is clean and document-like. The three semantic colors — green (open), red (closed), purple (merged) — form a universally understood status language across the platform.

## 2. Color System
GitHub maintains full light/dark parity through the Primer color system:
- **Light canvas**: #ffffff / #F6F8FA (alt surfaces)
- **Dark canvas**: #0d1117 / #161B22 (elevated panels)
- **Accent blue**: #0969DA — the primary interactive color for links, buttons, focus rings
- **Success/Open**: #1a7f37 — open PRs, successful CI checks
- **Danger/Closed**: #CF222E — closed issues, failed builds
- **Merged**: #8250DF — the unique purple for merged pull requests
- **Muted text**: #636C76 (light) / #8B949E (dark)

## 3. Typography
Mona Sans is GitHub's variable font, used on marketing and homepage at heavy weights (800). Product UI uses the system stack (-apple-system, Segoe UI) at 14px body. Code is always set in ui-monospace/SFMono-Regular. The typographic hierarchy is functional rather than expressive — clear sizing and weight contrast without decorative lettering.

## 4. Components & Patterns
- **Repository cards**: Bordered containers, name in blue link weight, description muted, language dot + star count in footer
- **Issues/PRs**: Green/red/purple status badges always lead the list item
- **Code blocks**: Monospace, #F6F8FA background, line numbers optional, syntax highlighted
- **Buttons**: Primary solid blue, secondary outlined gray, danger outlined red
- **Navigation**: Horizontal tab row with underline indicator, very thin 1px border-bottom
- **Labels**: Pill-shaped, user-defined colors, small font size

## 5. Spacing & Layout
Container max-width 1280px, centered. Repository layout: 260px sidebar (file tree/repo meta), flexible content area. Issues list uses a table-like approach: full-width rows, 1px border between. Tight vertical density — GitHub respects that developers want to see more, not fewer, items per screen.

## 6. Motion & Interaction
Restrained. Hover states change background color (not scale or transform). Page transitions are browser-native. The contribution graph animates on first load. Tooltips appear with 100ms delay, no animation. The focus is on data, not delight.

## Rationale

**Three semantic states as a universal language** — The green/red/purple triad for open/closed/merged isn't just a visual system — it's a communication protocol understood by millions of developers globally. Purple for "merged" is unusual enough to be distinct (not confused with any other state) while semantically appropriate (blending the green and red of open and closed). The system became a lingua franca because it was designed to be unambiguous at a glance.

**14px body as density investment** — GitHub's decision to render product UI at 14px reflects that developers are expert users who scan repositories, issue lists, and pull request diffs with professional efficiency. Smaller body text lets more code context appear before a scroll, which directly reduces the number of actions needed to review a pull request.

**Mona Sans variable font at heavy marketing weights** — GitHub uses Mona Sans at 800 weight only on the homepage and marketing contexts, while the product uses system fonts. This separation clearly signals which surfaces are "GitHub brand" and which are "GitHub tool" — a useful distinction as GitHub serves both developers evaluating the platform and developers using it daily.

**Dark mode as developer-native context** — GitHub's dark mode (#0d1117) borrows from terminal tradition — the environment where code actually runs. By matching the dark context that developers associate with "working," dark mode reduces the cognitive dissonance of switching between GitHub and the terminal or IDE. It's a contextual alignment choice, not just a visual preference.

**Primer design system as open-source commitment** — Publishing the design system open-source is both a trust signal and a developer relations strategy: it demonstrates GitHub's values (transparency, community) and lets enterprise customers audit the accessibility compliance of the components their developers use daily.

## Accessibility

### Contrast Ratios
- **Primary on background** (#0969DA on #ffffff): 4.6:1 — passes AA, fails AAA
- **Text on surface** (#1F2328 on #ffffff): 17.9:1 — passes AA
- **Muted on background** (#636C76 on #ffffff): 5.7:1 — passes AA (decorative)

### Minimum Requirements
- **Touch target**: 44×44px minimum for all interactive elements
- **Focus indicator**: #0969DA outline, 2px, 2px offset
- **Focus contrast**: 4.6:1 against #ffffff background

### Motion
- Respects `prefers-reduced-motion`: yes — contribution graph load animation should be suppressed; tooltips appear instantly
- All transitions use `@media (prefers-reduced-motion: reduce)` guard

### Notes
- Primer design system has strong accessibility foundations; GitHub ships accessible-by-default components as part of their open-source commitment
- Semantic status colors (green open, red closed, purple merged) rely on color alone in list views — icon or text labels must accompany these colors for color-blind users
- Dark mode: ink (#E6EDF3) on dark canvas (#0d1117) yields approximately 14.5:1 — passes AAA; dark mode is an excellent accessibility context
- Muted dark ink (#8B949E on #0d1117): approximately 5.9:1 — passes AA; adequate for secondary metadata in dark mode
