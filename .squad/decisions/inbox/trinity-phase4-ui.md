### 2026-05-10: Phase 4 UI Decisions
**By:** Trinity (Blazor Dev)
**Session:** Phase 4 UI implementation

**Decisions:**

1. **Route aliases**: New routes (`/audit-runs/*`, `/reviewer-queue`, `/audit-runs/{id}/categories/{categoryId}`) stay alongside the earlier `/audits/*`, `/reviewer/queue`, and `/review/{categoryId}` routes to avoid breaking existing navigation.
2. **CSS ownership**: Layout and component styling remains centralized in `wwwroot/css/`; legacy `.razor.css` files for the layout now only point contributors back to the design-system stylesheets.
3. **Placeholder screens**: Pages without a service contract yet use clearly labeled demo data or empty states inside the page component rather than introducing speculative service abstractions.
4. **Score bars**: Horizontal score bars now render with semantic `<progress>` markup so the UI stays accessible and avoids inline `style=` attributes in Razor.
5. **Role-gated shell**: Sidebar visibility follows the chartered role matrix using `<AuthorizeView>` rather than code-behind rendering checks.
6. **Telemetry access**: `/telemetry` is reserved for `SuperAdmin` only to match the screen inventory.
7. **Category review**: Reviewer actions stay wired to `IReviewerWorkflow`; rejection reasons come from reviewer comments so the UI does not invent a separate domain concept.
