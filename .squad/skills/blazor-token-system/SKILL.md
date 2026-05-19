# Skill: Blazor CSS Token System

## Summary
How to set up and maintain the design token cascade in this Blazor app.

## Load Order
```html
<link rel="stylesheet" href="css/tokens.css" />
<link rel="stylesheet" href="css/components.css" />
<link rel="stylesheet" href="css/app.css" />
```

## Convention
- `tokens.css` defines `:root` custom properties only — no selectors, no rules
- Primitive tokens: `--color-navy-800`, `--color-slate-200`, etc.
- Semantic tokens: `--text-primary`, `--accent`, `--bg-page` — map to primitives
- Components reference only semantic tokens; never hardcode hex in `.razor` or `.css`
- Scoped `.razor.css` files can use both semantic and primitive tokens

## Adding a new token
1. Add the primitive in the appropriate scale block in `tokens.css`
2. If it needs a semantic alias, add that in the "Semantic Tokens" section
3. Reference the semantic token in component CSS

## Icon components
Inline SVG components live in `Components/Icons/`. Each:
- Has a `Size` parameter (default 20)
- Uses `currentColor` for stroke (inherits CSS color)
- Is Lucide-style: 24×24 viewBox, stroke-width 1.5, no fill

## Shell composition
See `.squad/agents/trinity/history.md` for shell composition diagram.
