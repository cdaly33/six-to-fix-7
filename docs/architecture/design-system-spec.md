# CSS Design System Specification — StrategicGlue Six-to-Fix

> **Version:** 1.0  
> **Author:** Trinity (Blazor Dev)  
> **Date:** 2026-05-10  
> **Status:** Locked — gates all UI development  
>
> This is the authoritative reference for all CSS tokens, typography, spacing, and component patterns. Every component works from this file. Nothing in the UI is hardcoded — every value references a token defined here.

---

## Section 1: Color Tokens

All tokens are declared in `:root` in `wwwroot/css/design-system.css`. No component references a raw color value.

```css
:root {
  /* ── Background ──────────────────────────────────────────── */
  --bg-primary:    #F5F0E8;   /* warm cream — page background */
  --bg-secondary:  #EDE8DF;   /* slightly darker warm — table headers, alt rows */
  --bg-dark:       #1A1A2E;   /* deep navy — top nav bar, dark panels */
  --bg-card:       #FFFFFF;   /* white — card surfaces */
  --bg-sidebar:    #F0EBE1;   /* warm off-white — sidebar background */
  --bg-overlay:    rgba(26, 26, 46, 0.48); /* dark overlay — drawers, modals */

  /* ── Text ────────────────────────────────────────────────── */
  --text-primary:  #1A1A2E;   /* deep navy — body text on light bg */
  --text-secondary:#4A4A6A;   /* muted navy — supporting labels */
  --text-muted:    #8A8AAA;   /* lighter — hints, placeholders, disabled */
  --text-inverse:  #F5F0E8;   /* warm cream — text on dark bg */

  /* ── Accent / Action ─────────────────────────────────────── */
  --accent:         #2563EB;  /* blue — primary actions, active states */
  --accent-hover:   #1D4ED8;  /* darker blue — hover on accent elements */
  --accent-light:   #EFF6FF;  /* very light blue — accent tint bg */
  --accent-contrast:#FFFFFF;  /* white — text on accent background */

  /* ── Semantic ────────────────────────────────────────────── */
  --color-success:       #16A34A;
  --color-success-light: #DCFCE7;
  --color-warning:       #D97706;
  --color-warning-light: #FEF3C7;
  --color-error:         #DC2626;
  --color-error-light:   #FEE2E2;
  --color-info:          #0891B2;
  --color-info-light:    #E0F2FE;

  /* ── Borders ─────────────────────────────────────────────── */
  --border-default: #E5E0D5;  /* warm sand — standard borders */
  --border-focus:   #2563EB;  /* accent blue — focus rings */
  --border-error:   #DC2626;  /* red — error state borders */

  /* ── Score Tier Colors ───────────────────────────────────── */
  --tier-1-color:       #16A34A; /* green — high maturity (tier_1) */
  --tier-1-color-light: #DCFCE7;
  --tier-2-color:       #2563EB; /* blue — developing maturity (tier_2) */
  --tier-2-color-light: #EFF6FF;
  --tier-3-color:       #D97706; /* amber — early stage (tier_3) */
  --tier-3-color-light: #FEF3C7;
}
```

### Color Usage Rules

- **Never** reference a hex value outside of `:root`. All components use `var(--token-name)`.
- **`--bg-dark`** is used only for the top navigation bar and explicitly dark-mode panels. The rest of the UI is warm cream.
- **Score badge coloring** follows a hard mapping: scores 0–3 → `--color-error`, 4–6 → `--color-warning`, 7–10 → `--color-success`.
- **`--bg-overlay`** is applied by `.drawer-overlay` and `.modal-overlay` — not inline.

---

## Section 2: Typography

```css
:root {
  /* ── Font Families ───────────────────────────────────────── */
  --font-primary: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto,
                  "Helvetica Neue", Arial, sans-serif;
  --font-mono:    "Cascadia Code", "Fira Code", "Consolas", "Monaco",
                  "Courier New", monospace;

  /* ── Font Scale (1rem = 16px base) ──────────────────────── */
  --text-xs:   0.75rem;   /* 12px — badges, captions, timestamps */
  --text-sm:   0.875rem;  /* 14px — table cells, hints, secondary labels */
  --text-base: 1rem;      /* 16px — body text, input values */
  --text-lg:   1.125rem;  /* 18px — card titles, section subheadings */
  --text-xl:   1.25rem;   /* 20px — page subheadings */
  --text-2xl:  1.5rem;    /* 24px — page headings */
  --text-3xl:  1.875rem;  /* 30px — hero headings */
  --text-4xl:  2.25rem;   /* 36px — display text */

  /* ── Font Weights ────────────────────────────────────────── */
  --font-normal:   400;
  --font-medium:   500;
  --font-semibold: 600;
  --font-bold:     700;

  /* ── Line Heights ────────────────────────────────────────── */
  --leading-tight:   1.25;  /* headings */
  --leading-normal:  1.5;   /* body text */
  --leading-relaxed: 1.75;  /* readable long-form text */

  /* ── Letter Spacing ──────────────────────────────────────── */
  --tracking-tight:  -0.025em; /* large headings */
  --tracking-normal:  0em;     /* body */
  --tracking-wide:    0.05em;  /* table headers (uppercase labels) */
}
```

### Typography Rules

- Base font size is **14px** on the `<html>` element (set in `app.css`), meaning 1rem = 14px for this project. The scale values above are relative to that base.
- Table column headers use `--text-sm`, `--font-semibold`, `text-transform: uppercase`, `--tracking-wide`.
- Page titles use `--text-2xl`, `--font-bold`, `--leading-tight`.
- `--font-mono` is used for skill output previews, JSON display, and code-adjacent content only.

---

## Section 3: Spacing Scale

4px base unit. Token names map to multiples of 4px.

```css
:root {
  --space-1:  0.25rem;   /*  4px */
  --space-2:  0.5rem;    /*  8px */
  --space-3:  0.75rem;   /* 12px */
  --space-4:  1rem;      /* 16px */
  --space-5:  1.25rem;   /* 20px */
  --space-6:  1.5rem;    /* 24px */
  --space-8:  2rem;      /* 32px */
  --space-10: 2.5rem;    /* 40px */
  --space-12: 3rem;      /* 48px */
  --space-16: 4rem;      /* 64px */
  --space-20: 5rem;      /* 80px */
  --space-24: 6rem;      /* 96px */
  --space-28: 7rem;      /* 112px */
  --space-32: 8rem;      /* 128px */
  --space-40: 10rem;     /* 160px */
  --space-48: 12rem;     /* 192px */

  /* ── Border Radii ────────────────────────────────────────── */
  --radius-sm:   4px;
  --radius-md:   8px;    /* standard cards, inputs */
  --radius-lg:   12px;
  --radius-xl:   16px;
  --radius-full: 9999px; /* pills, badges, avatars */
}
```

---

## Section 4: Shadows & Elevation

```css
:root {
  --shadow-sm:  0 1px 3px rgba(0, 0, 0, 0.08);                         /* cards at rest */
  --shadow-md:  0 4px 6px rgba(0, 0, 0, 0.07), 0 1px 3px rgba(0,0,0,0.06);  /* hover lift */
  --shadow-lg:  0 10px 15px rgba(0, 0, 0, 0.07), 0 4px 6px rgba(0,0,0,0.05); /* drawers, modals */
  --shadow-xl:  0 20px 25px rgba(0, 0, 0, 0.08), 0 8px 10px rgba(0,0,0,0.04); /* elevated overlays */
}
```

**Elevation rules:**
- Cards at rest: `--shadow-sm`
- Cards on hover: `--shadow-md` (with `transform: translateY(-1px)`)
- Category Review Drawer: `--shadow-lg`
- Confirmation modal: `--shadow-xl`

---

## Section 5: Component Patterns

These are CSS class conventions and visual specifications. Razor component code is not defined here.

---

### Buttons

All buttons share a base `.btn` class for reset, cursor, and transition. Size and style modifiers are additive.

| Class | Visual Spec |
|---|---|
| `.btn-primary` | `--accent` background, `--accent-contrast` text, `--radius-md`, `--space-3` v-padding, `--space-6` h-padding |
| `.btn-secondary` | `--bg-card` background, `--border-default` border, `--accent` text |
| `.btn-ghost` | Transparent background, no border, `--text-secondary` text → `--accent` on hover |
| `.btn-danger` | `--color-error` background, white text |

**Size modifiers:**
- `.btn-sm`: `--text-sm`, `--space-2` v, `--space-4` h
- `.btn-md`: `--text-base`, `--space-3` v, `--space-6` h (default)
- `.btn-lg`: `--text-lg`, `--space-4` v, `--space-8` h

**Required states:**
- `:hover` — darkens background or shifts text color
- `:active` — slight scale-down (`transform: scale(0.98)`)
- `:disabled` — 50% opacity, `cursor: not-allowed`, no pointer events
- `:focus-visible` — `outline: 2px solid var(--border-focus)`, `outline-offset: 2px` — **mandatory for a11y**

---

### Form Inputs

| Class | Usage |
|---|---|
| `.input-field` | Text inputs, selects, textareas. `--border-default` border, `--radius-md`, `--space-3` padding, `--bg-card` background. Focus: `border-color: var(--border-focus)`, `box-shadow: 0 0 0 3px var(--accent-light)` |
| `.input-field.input-error` | Replaces border with `--border-error`, adds error shadow in `--color-error-light` |
| `.input-label` | Block element above input. `--font-medium`, `--text-sm`, `--text-primary`, `--space-2` margin-bottom |
| `.input-hint` | Block element below input. `--text-muted`, `--text-sm`, `--space-1` margin-top |
| `.input-error-message` | Block element below input, visible only on validation failure. `--color-error`, `--text-sm`, `--font-medium` |

Inputs are always full-width within their container. Min-height for textareas: 80px.

---

### Cards

```
.card
  .card-header   ← title (h2/h3) + optional action slot (top-right)
  .card-body     ← primary content
  .card-footer   ← optional: action buttons, secondary info
```

| Property | Value |
|---|---|
| Background | `--bg-card` |
| Border | `1px solid var(--border-default)` |
| Border radius | `--radius-md` |
| Shadow | `--shadow-sm` |
| Padding | `--space-6` (card body) |
| Hover | `--shadow-md`, `translateY(-1px)`, transition `--transition-normal` |

`.card-header` has `--space-4` bottom border and includes a flex layout: title left, action slot right.

---

### Data Tables

| Element | Spec |
|---|---|
| `.data-table` | `width: 100%`, `border-collapse: collapse` |
| `.data-table th` | `--bg-secondary`, `--font-semibold`, `--text-sm`, `text-transform: uppercase`, `--tracking-wide`, `--text-secondary`, `--space-3` padding |
| `.data-table td` | `border-bottom: 1px solid var(--border-default)`, `--space-4` padding, `--text-base` |
| `.data-table tr:hover td` | `background: var(--bg-secondary)` |
| `.th-sortable` | Adds sort indicator arrow (`▲` / `▼` / neutral) via `::after` pseudo-element; cursor pointer |

Sticky table header (`.data-table-scroll-container`) uses `max-height` + `overflow-y: auto` with `position: sticky; top: 0` on `thead`.

---

### Score Badges

`.score-badge` — a pill/circle element displaying a numeric score 0–10.

| Score Range | Color Token |
|---|---|
| 0–3 | `--color-error` background, white text |
| 4–6 | `--color-warning` background, white text |
| 7–10 | `--color-success` background, white text |

Size: 32px × 32px circle (or auto-width pill for two-digit scores). Font: `--font-bold`, `--text-sm`.

---

### Tier Badges

`.tier-badge` — a pill badge indicating the audit's tier recommendation.

| Class | Color | Label |
|---|---|---|
| `.tier-badge.tier-1` | `--tier-1-color` background, `--tier-1-color-light` bg on light variant | Tier 1 |
| `.tier-badge.tier-2` | `--tier-2-color` background, `--tier-2-color-light` bg on light variant | Tier 2 |
| `.tier-badge.tier-3` | `--tier-3-color` background, `--tier-3-color-light` bg on light variant | Tier 3 |

Padding: `--space-1` v, `--space-3` h. Border radius: `--radius-full`. Font: `--font-semibold`, `--text-xs`, uppercase.

---

### Progress Bars

`.progress-bar` — used in Skill Chain Runner to show 0–100% skill execution progress.

```
.progress-bar-track   ← full-width track, --bg-secondary, --radius-full
  .progress-bar-fill  ← width set via inline style (CSS custom property --pct), --accent bg, --radius-full
```

Animated fill with `transition: width var(--transition-normal)`. When a skill is `running`, the fill pulses with `@keyframes progress-pulse`.

---

### Drawers

`.drawer` — slides in from the right side. Used for Category Review Drawer.

```
.drawer-overlay   ← fixed full-viewport, --bg-overlay, z-index 40
.drawer           ← fixed right-0, full height, --bg-card, --shadow-lg,
                     width: 480px (desktop) / 100vw (mobile), z-index 50
  .drawer-header  ← title + close button, border-bottom
  .drawer-body    ← scrollable content, --space-6 padding
  .drawer-footer  ← action buttons, border-top, --space-4 padding
```

Slide-in animation: `transform: translateX(100%)` → `translateX(0)` over `--transition-slow`.

---

### Status Indicators

`.status-dot` — a 10px × 10px circle, inline-block, `--radius-full`.

| State | Color | Animation |
|---|---|---|
| `running` | `--accent` | `@keyframes pulse-ring` — expand + fade ring |
| `completed` | `--color-success` | None |
| `failed` | `--color-error` | None |
| `pending` | `--text-muted` | None |

---

## Section 6: Animation & Motion

```css
:root {
  --transition-fast:   100ms ease;
  --transition-normal: 200ms ease;
  --transition-slow:   300ms ease;
}

/* Skill running indicator */
@keyframes pulse-ring {
  0%   { box-shadow: 0 0 0 0 rgba(37, 99, 235, 0.4); }
  70%  { box-shadow: 0 0 0 8px rgba(37, 99, 235, 0); }
  100% { box-shadow: 0 0 0 0 rgba(37, 99, 235, 0); }
}

/* Progress bar shimmer while running */
@keyframes progress-pulse {
  0%, 100% { opacity: 1; }
  50%       { opacity: 0.75; }
}

/* Drawer slide-in */
@keyframes drawer-enter {
  from { transform: translateX(100%); }
  to   { transform: translateX(0); }
}

/* Toast notification */
@keyframes toast-enter {
  from { transform: translateY(100%); opacity: 0; }
  to   { transform: translateY(0);    opacity: 1; }
}
@keyframes toast-exit {
  from { transform: translateY(0);    opacity: 1; }
  to   { transform: translateY(100%); opacity: 0; }
}
```

**Motion rules:**
- All interactive element transitions use `--transition-normal` (200ms) unless fast feedback is required.
- The drawer uses `--transition-slow` (300ms) — it's a large surface and abrupt appearance is jarring.
- Pulse animations run only on `running` state — stop immediately when state changes.
- Honor `prefers-reduced-motion`: wrap all non-essential animations in a `@media (prefers-reduced-motion: no-preference)` block.
