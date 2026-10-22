---
name: תלמידון (Talmidon)
description: A sunlit study room for private tutors, their families and students — Hebrew, right-to-left, warm paper and teal ink.
colors:
  primary: "#0f766e"
  primary-hover: "#115e59"
  primary-bright: "#14b8a6"
  primary-dark-mode: "#2dd4bf"
  hero-deep: "#115e59"
  hero-cyan: "#155e75"
  accent: "#f59e0b"
  accent-soft: "#fef3c7"
  accent-text: "#92400e"
  paper: "#f5f5f4"
  card: "#ffffff"
  border-warm: "#e7e5e4"
  ink: "#292524"
  ink-muted: "#78716c"
  night: "#0f151b"
  night-card: "#171f27"
  night-border: "#2c3743"
  night-ink: "#e4e9ee"
  night-ink-muted: "#9aa8b6"
  success-soft: "#dcfce7"
  success-text: "#166534"
  info-soft: "#dbeafe"
  info-text: "#1e40af"
  danger-text: "#b91c1c"
typography:
  display:
    fontFamily: "Heebo, system-ui, -apple-system, Segoe UI, sans-serif"
    fontSize: "clamp(1.9rem, 4.5vw, 2.6rem)"
    fontWeight: 800
    lineHeight: 1.2
    letterSpacing: "-0.015em"
  headline:
    fontFamily: "Heebo, system-ui, sans-serif"
    fontSize: "1.6rem"
    fontWeight: 800
    letterSpacing: "-0.015em"
  title:
    fontFamily: "Heebo, system-ui, sans-serif"
    fontSize: "1.125rem"
    fontWeight: 600
  metric:
    fontFamily: "Heebo, system-ui, sans-serif"
    fontSize: "2rem"
    fontWeight: 800
    lineHeight: 1.15
    fontFeature: "tnum"
  body:
    fontFamily: "Heebo, system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.5
  label:
    fontFamily: "Heebo, system-ui, sans-serif"
    fontSize: "0.85rem"
    fontWeight: 500
rounded:
  xs: "3px"
  sm: "6px"
  md: "10px"
  lg: "14px"
  xl: "18px"
  full: "9999px"
spacing:
  xs: "0.5rem"
  sm: "0.75rem"
  md: "1.25rem"
  lg: "1.5rem"
  xl: "2rem"
  page-bottom: "4rem"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.card}"
    rounded: "{rounded.md}"
    padding: "0.625rem 1.1rem"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
  card:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.xl}"
    padding: "1.5rem"
  input:
    backgroundColor: "{colors.card}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    padding: "0.625rem 0.875rem"
  tag:
    rounded: "{rounded.xl}"
    padding: "0.2rem 0.65rem"
  stat-card:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.xl}"
    padding: "1.25rem"
  dialog:
    backgroundColor: "{colors.card}"
    rounded: "{rounded.xl}"
---

# Design System: תלמידון (Talmidon)

<!-- Source of truth for values: frontend/src/app/core/theme/talmidon-preset.ts (PrimeNG Aura preset) and frontend/src/styles.scss. Components never hardcode colors; they read --p-* and --p-talmidon-* tokens. -->

## Overview

**Creative North Star: "The Sunlit Study Room"**

Talmidon feels like a well-lit room where a lesson happens: warm stone-toned paper on the table, teal ink for what matters, a touch of amber for what needs attention. It is a Hebrew, right-to-left product first. Layout, icon direction and logical properties (`inset-inline`, `margin-inline-start`) assume RTL and are never a mirrored afterthought.

The room changes with who is in it. The public library and the landing pages, where a family is still deciding, get the sunlight: a deep teal-to-cyan hero gradient, soft glow behind the auth card, cards that lift on hover, staggered rise-in motion. The tutor's working screens (calendar, students, billing, notes) are the desk: the same palette and shapes, but quieter, denser and built for scanning. Each component is tuned to its place rather than to one global mood.

Dark mode is a designed second room, not an inversion: a deep blue-charcoal surface scale replaces the warm stone, primary brightens to teal 400, and shadows deepen so cards still separate from the page.

**Key Characteristics:**
- Warm paper surfaces (stone) under a cool teal brand color.
- One secondary accent (amber) reserved for money and reminders.
- Generously rounded corners (10–18px) and soft, layered shadows.
- Heebo throughout; heavy 800 weight for headlines and numbers.
- Expressive on public surfaces, restrained on working surfaces.
- Light and dark both first-class, switched by the `.app-dark` class.

## Colors

Warm neutrals carry the page; teal speaks for the brand; amber speaks only when something is owed or pending.

### Primary
- **Study-Room Teal** (`primary`): Buttons, links, focus rings, active navigation, page-header icons, empty-state icons. Teal 700 in light mode because white text on teal 600 fails AA; teal 400 (`primary-dark-mode`) in dark mode with dark text on it.
- **Deep Lagoon to Harbor Cyan** (`hero-deep` → `primary` → `hero-cyan`): The three-stop public hero gradient. Edges stay dark enough that white text passes AA across the full width.
- **Bright Teal** (`primary-bright`): Only the top of the brand-mark gradient.

### Secondary
- **Lesson-Bell Amber** (`accent`, `accent-soft`, `accent-text`): Charges, open balances, reminders, pending requests, the second glow on the auth background. Text on amber-soft uses `accent-text`, never `accent`.

### Neutral
- **Warm Paper** (`paper`): App and page background (stone 100). Separates the page from white cards without a border.
- **Card White** (`card`): Cards, dialogs, search bar, form fields.
- **Pencil Line** (`border-warm`): Dividers, glass header border, card borders.
- **Ink** (`ink`) / **Faded Ink** (`ink-muted`): Body text and secondary text (stone 800 / 500).
- **Night Study** (`night`, `night-card`, `night-border`, `night-ink`, `night-ink-muted`): The custom dark scale. Muted text in dark mode is surface 300, because 400 falls under 4:1 on cards.

### Status
- **Success / Info / Danger** (`success-*`, `info-*`, `danger-text`): Soft tinted backgrounds with a dark text tone of the same hue. Field errors use `danger-text` (red 700), never PrimeFlex red 500, which only reaches 3.8:1 on white.

### Named Rules
**The No-Hex Rule.** No component or stylesheet hardcodes a color. Every color is a `--p-*` or `--p-talmidon-*` token so dark mode works without a second stylesheet.

**The Amber Means Money Rule.** Amber marks things that are owed, pending or waiting for action. It is never decoration.

**The Tint, Don't Paint Rule.** Status and category color appears as a 12–18% `color-mix` tint behind an icon or badge, or a 3px bar on a card edge. Whole cards are never filled with a status color.

## Typography

**Display Font:** Heebo (with system-ui, -apple-system, Segoe UI, sans-serif)
**Body Font:** Heebo
**Label/Mono Font:** ui-monospace only for code-like values (e.g. tokens, IDs)

**Character:** One Hebrew-first geometric sans, loaded at 300–800. Hierarchy comes from weight and size, not from a second face. Headlines are tight (-0.015em) and heavy; body is plain and calm.

### Hierarchy
- **Display** (800, clamp(1.9rem, 4.5vw, 2.6rem), 1.2): Public hero headline only. Balanced wrapping.
- **Headline** (800, 1.6rem): Page-header titles inside the app, paired with a teal icon.
- **Title** (600, 1.125rem): Card titles; dialog titles go to 700 at 1.15rem.
- **Metric** (800, 2rem, 1.15, tabular figures): Numbers on stat cards and the hero stats row. Digits must not shift when values update.
- **Body** (400, 1rem, 1.5): Running text. Hints and empty-state copy are capped at about 44–46ch.
- **Label** (500, 0.85–0.9rem): Field labels (500, text color), stat labels and hints (muted).

### Named Rules
**The Weight Carries Hierarchy Rule.** Change weight before changing size. Headings and numbers sit at 800, titles at 600, labels at 500.

## Layout

A single centered content column, max 1140px, with 1.5rem side padding (0.9rem under 600px) and generous bottom space (4rem). The sticky glass top bar uses the same 1140px width so the bar and content edges align.

Grids are content-driven: the tutor library uses `repeat(auto-fill, minmax(280px, 1fr))` with 1.25rem gaps, and cards in a row stretch to equal height with their price/link row pinned to the bottom. PrimeFlex utilities handle ad-hoc rows.

Breakpoints are practical, not a framework scale: 640px and 600px (phone), 420px (narrowest phones, the logo shrinks). On phones, dialogs cap at `100vw - 1.5rem`, the signup button collapses to its icon, and the top bar must never push the page wider than the screen.

**The One Row Rule.** The top bar stays on one line. Items compress or collapse to icons; they never wrap into a second row that looks intentional.

## Elevation & Depth

A layered system: white cards float on warm paper with soft, cool-tinted shadows (`rgba(15, 23, 30, …)`). Depth also comes from tone (paper vs. card) and from the frosted glass top bar (`backdrop-filter: saturate(180%) blur(12px)`). In dark mode every shadow is redefined deeper (black at 0.4–0.7) because the light-mode shadows disappear on charcoal.

### Shadow Vocabulary
- **Rest** (`--p-talmidon-shadow-sm`): Cards and stat tiles at rest, the brand mark.
- **Hover lift** (`--p-talmidon-shadow-md`): Interactive stat cards on hover.
- **Raised** (`--p-talmidon-shadow-lg`): Tutor cards on hover, the hero search bar.
- **Floating** (`--p-talmidon-shadow-xl`): Dialogs and the auth card.

### Named Rules
**The Lift on Intent Rule.** Clickable cards rise 3–4px with one shadow step up and a primary-tinted border on hover. Non-clickable cards never move.

## Shapes

Soft, generous corners throughout, larger than Aura's defaults: inputs and buttons at 10px, content panels, toasts and menus at 14px, cards, dialogs and tags at 18px, tooltips at 6px. Avatars and empty-state icon wells are full circles. The brand mark is a 10px-rounded teal gradient square.

Borders are rare and quiet: 1px warm or transparent lines that turn primary-tinted on hover. The 3px colored top bar on stat cards is the one structural accent line.

## Components

### Buttons
- **Shape:** Gently rounded (10px).
- **Primary:** Teal on white text, label weight 600, 1.1rem inline padding. Hover deepens to teal 800, active to 900.
- **Focus:** A 2px solid primary ring with a 2px offset, on every focusable element, not only PrimeNG ones.
- **Secondary / Text / Icon-only:** PrimeNG severities and variants; icon-only on phones in the top bar with tight 0.35rem padding.

### Tags / Chips
- **Style:** Pill-like (18px), weight 600, padding 0.2rem 0.65rem, PrimeNG severity colors.
- **Stat badges:** a 14% tint of the tile's tone with the tone as text.

### Cards / Containers
- **Corner Style:** 18px.
- **Background:** Card white on warm paper (dark: night card on night).
- **Shadow Strategy:** Rest shadow; lift on hover only when clickable.
- **Internal Padding:** 1.5rem body, 0.75rem gap; titles 1.125rem / 600.

### Stat Card (signature)
A metric tile: a 3px tone bar along the top edge, a 2.25rem tinted icon well, a muted label, a heavy tabular number, optional hint and badge. The tone (`--stat-tone`) is primary, accent, success, warn or info. Linked tiles lift on hover.

### Inputs / Fields
- **Style:** White field, 10px radius, roomier padding (0.625rem × 0.875rem) for touch and Hebrew.
- **Label:** above the field, weight 500, 0.4rem gap; fields spaced 1rem apart.
- **Error:** message below in `danger-text`.

### Navigation
- **App shell:** A sticky frosted-glass top bar (the menubar's own background is transparent), brand mark and wordmark at the start, menu items with 10px radius, icon actions at the end. The wordmark is never truncated.
- **Public header:** The same glass treatment, with login and signup on the end side.

### Empty State
Centered: a 3.5rem circular teal-tinted icon well, a 600-weight title, a muted hint capped at 44ch, and an optional action. It explains what will appear and how to start.

### Loading
Shimmering skeleton lines (warm in light, charcoal in dark) instead of "loading…" text.

## Do's and Don'ts

### Do:
- **Do** read every color from a `--p-*` or `--p-talmidon-*` token, and add new tokens to `talmidon-preset.ts` under both light and dark.
- **Do** write layout with logical properties (`inset-inline`, `margin-inline-start`) so RTL is native.
- **Do** keep white text only on teal 700 or darker; use dark text on teal 400 in dark mode.
- **Do** use tabular figures for every number that can change (money, counts, stats).
- **Do** respect `prefers-reduced-motion`. Motion is the 0.4s `talmidon-rise` ease-out (`cubic-bezier(0.22, 1, 0.36, 1)`) with staggered delays, and 0.18s state transitions.
- **Do** let public surfaces be warmer and more animated than the tutor's working screens.

### Don't:
- **Don't** hardcode hex values in components or `styles.scss`.
- **Don't** use amber for decoration; it signals money or a pending action.
- **Don't** fill whole cards with status color; tint an icon well, badge or edge bar instead.
- **Don't** use PrimeFlex `red-500` for error text (3.8:1 on white).
- **Don't** let the top bar wrap to two rows or push the page wider than the phone screen.
- **Don't** fabricate social proof (counts, ratings, testimonials) in hero stats or cards.
