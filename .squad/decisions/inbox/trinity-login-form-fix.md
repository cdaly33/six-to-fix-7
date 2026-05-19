# Decision: Login.razor — disable prerendering to fix empty-model validation

**Author:** Trinity  
**Date:** 2026-05-18T22:02:01-05:00  
**Status:** Applied (PR #40)

## Context

Production bug: `https://app-sixtofix-prod.azurewebsites.net/login` showed
"The Email field is required." and "The Password field is required." even
when both fields were visibly filled and the user clicked Submit.

## Root Cause

`Login.razor` used `@rendermode InteractiveServer` which defaults to
`prerender: true`. The page lifecycle is:

1. Server prerenders the form as static HTML (no Blazor circuit yet)
2. User fills in email + password into the static DOM inputs
3. Blazor SignalR circuit connects → component re-initialises → `_model = new LoginModel()` (empty)
4. User clicks Submit → EditForm validates the empty interactive model → required-field errors

The browser DOM showed filled inputs but the interactive circuit held a fresh
empty model — a stale-DOM / empty-model race condition inherent to prerendering
with bound form fields.

## Decision

Disable prerendering on `Login.razor`:

```diff
-@rendermode InteractiveServer
+@rendermode @(new InteractiveServerRenderMode(prerender: false))
```

With `prerender: false` the form is never emitted as static HTML. It only
appears once the Blazor circuit is live, so every keystroke flows directly
into `_model` — no race possible.

## Implications for team

- **Rule:** Any Blazor page with `@bind-Value` user inputs under
  `@rendermode InteractiveServer` should use `prerender: false` unless
  there is an explicit SEO/TTFB requirement.
- Login, registration, and data-entry pages are the highest-risk candidates.
- `prerender: true` is safe for read-only display pages (no bound inputs).
- Logout.razor may warrant the same treatment if it ever gains a form.

## PR

https://github.com/cdaly33/six-to-fix-7/pull/40
