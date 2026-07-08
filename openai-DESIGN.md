---
version: alpha
name: OpenAI
description: "A stark, maximally minimal canvas anchored in true white (#FFFFFF) and near-black (#212121), with a single emerald-green accent (#10A37F) that appears only on the ChatGPT brand mark and key CTAs. The system is almost aggressively undecorated — no gradients, no photography, no illustrative elements beyond the logo — trusting negative space and precise type to carry all weight. Söhne (Klim Type) sets the display hierarchy at tight tracking and high contrast weights, while body text uses system-ui for crisp screen rendering. The restraint communicates confidence: OpenAI's visual language says it doesn't need to try."

colors:
  primary: "#10A37F"
  on-primary: "#ffffff"
  primary-hover: "#0E9270"
  ink: "#212121"
  ink-muted: "#6E6E80"
  canvas: "#FFFFFF"
  surface-1: "#F7F7F8"
  surface-2: "#ECECF1"
  border: "#E5E5E5"
  canvas-dark: "#212121"
  surface-dark-1: "#2A2A2A"
  surface-dark-2: "#343541"
  ink-dark: "#ECECF1"
  ink-muted-dark: "#8E8EA0"

typography:
  display:
    fontFamily: "Söhne, ui-sans-serif, system-ui, sans-serif"
    fontSize: 56px
    fontWeight: 600
    lineHeight: 1.07
    letterSpacing: -0.025em
  body:
    fontFamily: "Söhne, ui-sans-serif, system-ui, sans-serif"
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.625
    letterSpacing: 0

spacing:
  base: 8px
  scale: [4, 8, 12, 16, 24, 32, 48, 64, 96, 128]

radius:
  sm: 4px
  md: 8px
  lg: 16px
  pill: 9999px

shadows:
  card: "0 0 0 1px rgba(0,0,0,0.08)"
  elevated: "0 4px 12px rgba(0,0,0,0.1)"

motion:
  duration-fast: 100ms
  duration-base: 200ms
  easing: cubic-bezier(0.4, 0, 0.2, 1)
---

## 1. Visual Theme & Atmosphere
OpenAI's interface is a study in confident minimalism. The homepage is nearly all white with large, tightly tracked type — Söhne at scale — and one purposeful green. ChatGPT's product surface extends this into dark mode: a warm near-black (#343541) with white text bubbles and green avatars. There's no illustrative language, no stock photography, no decorative gradients. The brand bets everything on typography, whitespace, and product quality to speak for itself.

## 2. Color System
Binary palette with a single chromatic escape valve:
- **Canvas**: Pure #FFFFFF / dark #212121 — no warm or cool tint
- **Green accent**: #10A37F — used exclusively for the GPT logo, CTAs, and streaming indicators
- **Ink**: #212121 (light) / #ECECF1 (dark) — high contrast text
- **Muted**: #6E6E80 — captions, secondary labels, timestamps
- **Surfaces**: #F7F7F8 (sidebars) / #343541 (dark chat messages) — barely perceptible differentiation
- **Border**: #E5E5E5 — ultra-light dividers

## 3. Typography
Söhne by Klim Type Foundry is the backbone — a geometric grotesque derived from Futura but warmer and more humanistic. Used at 600 weight for headlines, 400 for body, with very tight tracking at large sizes (-0.025em). The choice reads as considered and premium without being showy. Monospace code uses native system fonts.

## 4. Components & Patterns
- **Chat bubbles**: User messages in light gray (#F7F7F8) or dark surface; assistant responses on white/dark canvas with no bubble — just text
- **Sidebar**: Collapsible conversation history, thin 1px right border, icon + label navigation items
- **Buttons**: Green primary (ChatGPT), black primary (API/enterprise), ghost secondary
- **Model picker**: Subtle dropdown in nav, monochrome icons
- **Code blocks**: Dark surface even in light mode, copy button top-right

## 5. Spacing & Layout
Chat layout: 260px sidebar, flex content area, message width capped at ~720px centered. Marketing pages use wide containers (1280px max) with 50% of the viewport left empty on hero sections. The space is intentional and communicative.

## 6. Motion & Interaction
Streaming text is the signature motion — character-by-character rendering that feels alive. UI chrome is static. Hover states are minimal color shifts. Sidebar expand/collapse is smooth but unshowy at 200ms ease.

## Rationale

**Aggressive minimalism as confidence signal** — In a market where competitors use dense hero sections, vibrant gradients, and product screenshots to explain themselves, OpenAI uses nearly empty white pages with large type. The restraint communicates that the product is so well-known it doesn't need to be explained — a luxury brand posture that only works when you're already the category leader.

**Green accent exclusively for GPT, nothing else** — #10A37F appears on the ChatGPT logo mark, streaming indicators, and primary CTAs. By never using it decoratively or in secondary contexts, OpenAI makes the color mean "AI response is happening here" — a semantic assignment that users internalize through repeated exposure to the streaming text experience.

**Söhne as a considered luxury choice** — Klim Type Foundry's Söhne is a Futura derivation with humanistic corrections — it's expensive, uncommon, and signals serious typographic investment. In a category where most products use Inter or system fonts, Söhne communicates that OpenAI thinks about aesthetics at the level of type foundry selection.

**Dark mode for ChatGPT, light for marketing** — The warm near-black chat interface (#343541) serves the reading experience of long AI responses better than white, where large text blocks can cause eye strain. The marketing site's white surface serves a different goal: conveying openness, transparency, and simplicity to prospective users evaluating whether to trust the product.

**No decorative imagery** — Every competitor uses photography, illustrations, or abstract visuals to make AI tangible. OpenAI's refusal to do so is a positioning bet: the text output is the product, and decorating around it would diminish its primacy. This constraint also prevents the visual identity from aging as AI aesthetics evolve.

## Accessibility

### Contrast Ratios
- **Primary on background** (#10A37F on #FFFFFF): 3.0:1 — fails AA for normal text, passes AA for large text (18px+)
- **Text on surface** (#212121 on #FFFFFF): 16.1:1 — passes AA
- **Muted on background** (#6E6E80 on #FFFFFF): 4.6:1 — passes AA (decorative)

### Minimum Requirements
- **Touch target**: 44×44px minimum for all interactive elements
- **Focus indicator**: #10A37F outline, 2px, 2px offset
- **Focus contrast**: 3.0:1 against #FFFFFF — supplementary non-color indicator (e.g. offset or box-shadow) required

### Motion
- Respects `prefers-reduced-motion`: yes — streaming text animation and sidebar transitions should halt; static text rendering on reduced-motion
- All transitions use `@media (prefers-reduced-motion: reduce)` guard

### Notes
- The green accent (#10A37F) fails AA at 3.0:1 on white for normal-sized body text — do not use it as the sole text color for labels or descriptive text; restrict it to large headings, icons, or graphical elements
- If green is used on CTA button text, the button must be at least 18px or 14px bold to pass AA for large text
- Dark mode surfaces (#343541, #2A2A2A) with light ink (#ECECF1) yield very high contrast (>12:1) — the dark mode palette is the stronger accessibility context
- Streaming text animation should be paused or replaced with instant reveal under `prefers-reduced-motion: reduce`
