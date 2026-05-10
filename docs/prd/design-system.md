# StrategicGlue Six-to-Fix — Design System Reference

> Version 1.0 | Product: Six-to-Fix | Platform: Blazor Server (.NET 10)
> This document is the single source of truth for visual design decisions. All UI components, screens, and layouts must conform to these specifications.

---

## 1. Color System

All colors are defined as CSS custom properties on `:root`. Component styles must reference tokens — never hard-code hex values.

### Background Tokens

| Token | Hex | Usage |
|---|---|---|
| `--bg-primary` | `#F5F0E8` | Main page background — warm cream |
| `--bg-secondary` | `#EDE7D9` | Card surfaces, panel fills — slightly darker cream |
| `--bg-elevated` | `#FFFFFF` | Modals, drawers, overlays — pure white |
| `--bg-dark` | `#1A1A2E` | Top navigation bar — dark navy |
| `--bg-info` | `#EBF4FF` | Informational banners |
| `--bg-warning` | `#FFFBEB` | Warning banners, stale state rows |
| `--bg-success` | `#F0FFF4` | Success banners |
| `--bg-danger` | `#FFF5F5` | Error/danger state backgrounds |

### Text Tokens

| Token | Hex | Usage |
|---|---|---|
| `--text-primary` | `#1A1A2E` | Body text — dark navy |
| `--text-secondary` | `#6B7280` | Labels, metadata, subtitles |
| `--text-muted` | `#9CA3AF` | Placeholders, disabled states |
| `--text-info` | `#1D4ED8` | Links, informational emphasis |
| `--text-success` | `#166534` | Success emphasis |
| `--text-warning` | `#92400E` | Warning emphasis |
| `--text-danger` | `#991B1B` | Error emphasis |

### Border Tokens

| Token | Hex | Usage |
|---|---|---|
| `--border` | `#E5E0D5` | Subtle warm gray — default card/section borders |
| `--border-strong` | `#C9C3B8` | Stronger — form inputs, table headers |

### Brand Accent

| Token | Hex | Usage |
|---|---|---|
| `--accent` | `#2563EB` | Primary action buttons, active states, focus rings |
| `--accent-hover` | `#1D4ED8` | Button hover state |

### Status / Badge Colors

| Purpose | Background Token | Hex | Text Token | Hex |
|---|---|---|---|---|
| Tier 1 (best) | `--tier1-bg` | `#D1FAE5` | `--tier1-text` | `#065F46` |
| Tier 2 | `--tier2-bg` | `#DBEAFE` | `--tier2-text` | `#1E40AF` |
| Tier 3 | `--tier3-bg` | `#FEF3C7` | `--tier3-text` | `#92400E` |
| Approved | `--approved-bg` | `#D1FAE5` | `--approved-text` | `#065F46` |
| Pending | `--pending-bg` | `#FEF3C7` | `--pending-text` | `#92400E` |
| Error | `--error-bg` | `#FEE2E2` | `--error-text` | `#991B1B` |

---

## 2. Typography Scale

**Font stack:** `system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`

No external font import required — the system stack ensures fast load and native rendering on all platforms.

### Size Scale

| Token | Value | Typical Use |
|---|---|---|
| `--text-xs` | 11px | Badge labels, footnotes, timestamps |
| `--text-sm` | 13px | Secondary metadata, table cells, helper text |
| `--text-base` | 14px | Default body text |
| `--text-md` | 16px | Card titles, sub-headings |
| `--text-lg` | 18px | Section headings |
| `--text-xl` | 22px | Page headings |
| `--text-2xl` | 28px | Hero/modal headings |

### Weight Scale

| Value | Name | Use |
|---|---|---|
| 400 | Normal | Body text, table data |
| 500 | Medium | Page titles, card headings |
| 600 | Semibold | Badge labels, button text, emphasis |
| 700 | Bold | Score numbers, critical callouts |

### Line Height

Default: `1.5` for all body text. Headings may use `1.2` for tighter vertical rhythm. Badge/pill text: `1` (no extra line height).

### Usage Guidelines

- Page heading (`<h1>` equivalent): `--text-xl`, weight 500, `--text-primary`
- Card title: `--text-md`, weight 600, `--text-primary`
- Body copy: `--text-base`, weight 400, `--text-primary`
- Metadata / labels: `--text-sm`, weight 400, `--text-secondary`
- Micro-labels (badges, pills): `--text-xs`, weight 600, uppercase
- Links: `--text-info`, underline on hover

---

## 3. Spacing System

### Spacing Scale

| Name | Value | Use |
|---|---|---|
| 4px | `0.25rem` | Tight gaps inside badges |
| 8px | `0.5rem` | Padding inside inputs, inline gaps |
| 12px | `0.75rem` | Small section gaps |
| 16px | `1rem` | Card padding, grid gaps between cards |
| 24px | `1.5rem` | Page horizontal padding, section vertical gap |
| 32px | `2rem` | Between major page sections |
| 48px | `3rem` | Hero/modal top margins |

### Layout Grid

- **Content max-width:** 1100px, horizontally centered via `margin: 0 auto`
- **Page horizontal padding:** 24px on left and right (so effective content width on a 1200px viewport = 1100px)
- **Section vertical gap:** 24px between major page sections
- **Card grid gap:** 16px between card items

### Responsive Breakpoints

| Name | Min Width | Behavior |
|---|---|---|
| Mobile | < 640px | Single-column layout, nav collapses to hamburger |
| Tablet | 640px–1023px | Two-column card grids, drawers stack |
| Desktop | ≥ 1024px | Full multi-column layouts |

---

## 4. Border Radius & Shadow Tokens

### Border Radius

| Token | Value | Use |
|---|---|---|
| `--radius-sm` | 4px | Pills, badges, chips |
| `--radius-md` | 8px | Cards, inputs, buttons, dropdowns |
| `--radius-lg` | 12px | Modals, drawers, large panels |

### Shadow Scale

| Token | Value | Use |
|---|---|---|
| `--shadow-sm` | `0 1px 3px rgba(0,0,0,0.08)` | Default card resting state |
| `--shadow-md` | `0 4px 12px rgba(0,0,0,0.10)` | Card hover state, dropdowns |
| `--shadow-lg` | `0 8px 24px rgba(0,0,0,0.12)` | Modals, drawers |

---

## 5. Component Specifications

### 5.1 Navigation Bar

- **Background:** `var(--bg-dark)` — `#1A1A2E`
- **Height:** 52px, full viewport width
- **Position:** fixed top, `z-index: 100`
- **Left side:** Logo mark + "Six-to-Fix" wordmark in white, 16px semibold
- **Center:** Primary nav links — white text, 14px, weight 500. Active link: white underline 2px solid `var(--accent)`, or distinct highlight
- **Right side:** Tenant switcher dropdown (Super Admin only) + User avatar chip (initials in a 32px circle, `--accent` background, white text) + dropdown (Profile, Sign Out)
- **Nav links:** Audits | Clients | Review Queue | Calibration | Telemetry | Admin (role-gated)

### 5.2 Page Header

- Sits immediately below the fixed nav bar
- Contains: page title (`--text-xl`, weight 500), optional subtitle (`--text-sm`, `--text-secondary`), right-aligned action button area
- Bottom border: `1px solid var(--border)`
- Padding: 20px 24px

### 5.3 Cards (General)

- **Background:** `var(--bg-secondary)` or `var(--bg-elevated)`
- **Border:** `1px solid var(--border)`
- **Border-radius:** `var(--radius-md)` — 8px
- **Padding:** 16px
- **Shadow:** `var(--shadow-sm)` at rest
- **Hover state:** shadow → `var(--shadow-md)`, border-color → `var(--border-strong)`
- **Transition:** `box-shadow 150ms ease, border-color 150ms ease`

### 5.4 Audit / Client Cards

- **Width:** 280px fixed; cards wrap in a flex row with 16px gap
- **Client name:** 16px, weight 600, `--text-primary`
- **Metadata line:** 12px, `--text-secondary` — formatted as `{industry} · {revenue band} · {date}`
- **Tier badge:** pill in top-right corner of card — `--tier1/2/3-bg/text`
- **Score display:** "Maturity: 24/60" and "Strategy: 1/6" — 14px, weight 500
- **Click target:** entire card navigates to Audit Detail

### 5.5 Score Area Cards (Six-Up Grid)

- **Layout:** 2 rows × 3 columns, 16px gap
- **Each card:** area name (16px, weight 600), activity score chip (0–10), documented strategy rating badge
- **Expandable:** click toggles expanded state showing: top gap item, evidence list, warning chips
- **Expanded state:** `border-color: var(--accent)`, shadow → `var(--shadow-md)`, smooth height transition
- **Warning chips:** amber pill (`--tier3-bg/text`) with warning code e.g. "LOW_CONFIDENCE"
- **Evidence strength badge:** red/amber/green pill depending on strength rating

### 5.6 Skill Chain Component

- **Layout:** vertical ordered list, full-width within its container
- **Row height:** 48px minimum
- **Each row contains (left to right):**
  - Step number circle (24px, `--bg-secondary`, `--text-secondary`, weight 600)
  - Skill name (`--text-base`, weight 500)
  - Status badge (see badge spec)
  - Action button (RUN / BLOCKED / STALE) right-aligned
- **Stale row:** background `var(--bg-warning)`, yellow-amber tint, "· Stale" appended to skill name
- **Blocked row:** action button disabled (opacity 0.5), tooltip shows dependency message
- **Running row:** spinner replaces action button, status badge shows "running"
- **Row separator:** `1px solid var(--border)`

### 5.7 Review Drawer / Slide Panel

- **Trigger:** clicking a reviewer queue item or a flagged skill
- **Position:** fixed, right edge of viewport, full height
- **Width:** 480px on desktop; full width on mobile
- **Background:** `var(--bg-elevated)` — white
- **Shadow:** `var(--shadow-lg)`
- **Overlay backdrop:** semi-transparent `rgba(0,0,0,0.3)` covers main content; clicking backdrop closes drawer
- **Header (60px):** category name (16px, weight 600) + current score + close (✕) button
- **Body:** scrollable — documented strategy elements, gaps, evidence list, benchmark context, AI explanation text
- **Footer (64px):** four action buttons — Approve (primary), Edit Score (secondary), Re-run (secondary), Escalate (danger)
- **Edit mode:** inline form with score number input + override reason code select + mandatory notes textarea
- **Locked state banner:** amber `--bg-warning` bar across header reading "Locked: reviewer edit in progress."

### 5.8 Buttons

| Variant | Background | Text | Border | Hover |
|---|---|---|---|---|
| Primary | `var(--accent)` | White | None | bg → `var(--accent-hover)`, `translateY(-1px)` |
| Secondary | White | `--text-primary` | `var(--border-strong)` | bg → `var(--bg-secondary)` |
| Danger | `var(--error-bg)` | `var(--error-text)` | `var(--error-text)` | darken slightly |
| Disabled | any, opacity 0.5 | any | any | cursor: not-allowed |

| Size | Height | Padding | Font size |
|---|---|---|---|
| sm | 28px | 4px 10px | 13px |
| md | 36px | 8px 16px | 14px (default) |
| lg | 42px | 10px 20px | 16px |

- Border-radius: `var(--radius-md)` — 8px on all buttons
- Transition: `background-color 120ms ease, transform 120ms ease`

### 5.9 Form Inputs

- **Border:** `1px solid var(--border-strong)`
- **Border-radius:** `var(--radius-md)` — 8px
- **Padding:** 8px 10px
- **Font:** 14px, `--text-primary`
- **Background:** `var(--bg-elevated)`
- **Focus ring:** `outline: 2px solid var(--text-info)`, `outline-offset: 1px`
- **Error state:** border-color → `var(--error-text)`, error message below in `--text-danger`, 12px
- **Disabled:** background `var(--bg-secondary)`, opacity 0.6, cursor not-allowed
- **Label:** `--text-sm`, weight 500, `--text-primary`, 4px below label to input

### 5.10 Badges / Pills

- **Border-radius:** `var(--radius-sm)` — 4px (slightly rounded, not fully circular)
- **Padding:** 2px 8px
- **Font:** 11px, weight 600, UPPERCASE
- **No border** — background provides contrast
- Color combinations: see Status / Badge Colors in Section 1

### 5.11 Sample / Demo Banner

- Position: sticky, sits below the fixed nav bar
- Background: `var(--bg-warning)`
- Text: `⚠️ Demo mode — showing sample data` — `--text-warning`, 13px, weight 500
- Padding: 8px 24px
- Displayed only when the app is running against sample data (feature-flag driven)

### 5.12 Toast Notifications

- **Position:** fixed, bottom-right, 16px from edges
- **Width:** 320px
- **Border-radius:** `var(--radius-md)`
- **Shadow:** `var(--shadow-md)`
- **Types:**
  - Success: `--bg-success`, left border 4px `--text-success`
  - Error: `--bg-danger`, left border 4px `--text-danger`
  - Info: `--bg-info`, left border 4px `--text-info`
- **Behavior:** slide in from right on appear, auto-dismiss after 4000ms, manual ✕ close
- **Stack:** multiple toasts stack vertically with 8px gap

### 5.13 Loading States

- **Layout:** centered content, minimum 200px height container
- **Content:** CSS spinner (24px, `--accent` color) + status message below (`--text-sm`, `--text-secondary`)
- **Markup:** `role="status"`, `aria-live="polite"`

### 5.14 Error States

- **Layout:** centered box with 16px padding
- **Content:** error icon + heading (`--text-danger`) + detail text (`--text-sm`, `--text-secondary`)
- **Markup:** `role="alert"`
- **Optional:** back/retry action button below

### 5.15 Empty States

- **Layout:** centered content block (illustration placeholder + text + CTA)
- **Heading:** `--text-lg`, weight 500, `--text-secondary`
- **CTA:** primary button below
- **Examples:** "No clients yet — Add your first client", "No items pending review"

---

## 6. Icon Usage

No specific icon library is mandated. Use inline SVG icons or a lightweight icon font. Icons must be accessible — include `aria-hidden="true"` on decorative icons and `aria-label` on standalone icon buttons.

### Required Icons (by screen area)

| Context | Icon needed |
|---|---|
| Nav bar | Home, Clients, Queue, Calibration, Chart/telemetry, Shield/admin |
| Audit cards | Arrow right (navigate), status indicators |
| Skill chain | Play (run), Block/lock, Warning triangle, Spinner |
| Review drawer | Checkmark (approve), Pencil (edit), Refresh (re-run), Arrow up (escalate), ✕ close |
| Forms | Eye/eye-off (password toggle), Upload (file), Delete/trash |
| Toasts | Checkmark, X, Info circle |
| Badges | Warning triangle for stale/error states |

Preferred sizing: 16px inline with text, 20px for standalone action icons, 24px for page-level decorative icons.

---

## 7. Motion & Transitions

Six-to-Fix uses subtle, purposeful motion only. No heavy animations or page transitions.

| Interaction | Transition |
|---|---|
| Card hover | `box-shadow 150ms ease, border-color 150ms ease` |
| Button hover | `background-color 120ms ease, transform 120ms ease` (lift 1px) |
| Score area card expand | `height` or max-height smooth expand — `250ms ease` |
| Drawer open | Slides in from right — `transform: translateX` — `200ms ease-out` |
| Toast appear | `transform: translateX(110%)` → `translateX(0)` — `200ms ease` |
| Focus ring | No transition — appears instantly for accessibility |
| Page loads | No route transition animation — content renders in place |

**Reduced-motion:** All transitions must respect `@media (prefers-reduced-motion: reduce)` — collapse to `transition: none`.

---

## 8. Accessibility Requirements

Target: **WCAG 2.1 AA compliance** across all screens.

### Contrast

- Body text on `--bg-primary` (`#1A1A2E` on `#F5F0E8`): ≈ 13:1 ✓
- Link color `--text-info` on white: must meet 4.5:1 — verify at design time
- Badges: all foreground/background combinations in the status palette must pass 4.5:1

### Focus Management

- All interactive elements receive a visible focus ring: `outline: 2px solid var(--text-info)`, `outline-offset: 1px`
- Tab order must follow visual reading order
- Drawer open: focus moves to drawer header/close button; drawer close: focus returns to trigger element
- Modal open: focus trapped inside modal until dismissed

### ARIA Guidance

- Navigation bar: `<nav aria-label="Main navigation">`
- Skill chain list: `role="list"`, each row `role="listitem"`
- Status badges: include screen-reader-visible text — do not rely solely on color
- Score area cards (expandable): `aria-expanded` toggles, `aria-controls` pointing to content region
- Review drawer: `role="dialog"`, `aria-modal="true"`, `aria-labelledby` pointing to header
- Toasts: `aria-live="polite"` for success/info, `aria-live="assertive"` for errors
- Loading indicators: `role="status"`, `aria-live="polite"`
- Error containers: `role="alert"`
- Form error messages: linked via `aria-describedby` to their input

### Keyboard Navigation

- Drawer: `Escape` key closes
- Modal: `Escape` closes; Tab/Shift+Tab cycles within
- Skill chain run button: activatable via `Enter` / `Space`
- Card grid: each card focusable, activated via `Enter`

---

## 9. Blazor Server Implementation Notes

### CSS Custom Properties Setup

Define all tokens in `wwwroot/css/app.css` (or equivalent global stylesheet) on the `:root` selector:

```
:root {
  --bg-primary:    #F5F0E8;
  --bg-secondary:  #EDE7D9;
  /* ... all tokens ... */
}
```

This file must be linked in `App.razor` or `_Host.cshtml` before any component-level styles.

### CSS Isolation (Component Scoping)

Each Blazor component that has its own layout styles should use a co-located `.razor.css` file:
- `AuditCard.razor` → `AuditCard.razor.css`
- `SkillChainRow.razor` → `SkillChainRow.razor.css`
- `ReviewDrawer.razor` → `ReviewDrawer.razor.css`

CSS isolation scopes styles to the component via the `b-{hash}` attribute selector. Use CSS custom properties (which pierce isolation boundaries) for design tokens, and use isolation files only for layout/structural rules.

### State-Driven Classes

Use `class="@GetRowClass()"` pattern in Razor for conditionally applied CSS classes based on component state (stale, blocked, running). Avoid inline style bindings for design tokens — reference `var(--token)` in the `.razor.css` file instead.

### Component-Level CSS Naming

Prefix component classes with a meaningful namespace to avoid collision with isolation hash fallback:
- `.sf-card`, `.sf-badge`, `.sf-drawer`, `.sf-skill-row`

Do not rely solely on element selectors in isolation files — class names make intent clear and survive refactoring.

### Responsive Handling

Use CSS media queries inside `.razor.css` files or in `app.css` for breakpoint-driven layout. Blazor Server renders SSR on initial load; ensure no FOUC (flash of unstyled content) by inlining critical CSS or loading the stylesheet in `<head>` synchronously.
