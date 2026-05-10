# Role-Based UI Visibility Matrix — StrategicGlue Six-to-Fix

> **Version:** 1.0  
> **Author:** Trinity (Blazor Dev)  
> **Date:** 2026-05-10  
> **Status:** Locked — gates all role-based rendering decisions  
>
> This is the authoritative reference for which UI elements appear for which roles. All role-based rendering decisions in Razor components reference this document. No ad-hoc role checks in component logic — every check maps to a defined rule here.

---

## Section 1: Roles

| Role | Description | Scope | JWT `roles` Claim Value |
|---|---|---|---|
| **SuperAdmin** | Platform operator. Cross-tenant access. Manages all tenants, system configuration, and global telemetry. | Global (all tenants) | `"SuperAdmin"` |
| **TenantAdmin** | Agency administrator. Full management within their own tenant. Can run audits, manage users, clients, and settings. | Single tenant | `"TenantAdmin"` |
| **Reviewer** | Quality gatekeeper. Reviews AI-generated category outputs and approves, edits, reruns, or escalates them. Can also upload documents. | Single tenant | `"Reviewer"` |
| **Viewer** | Read-only access. Can view published audit results and the audit list. Cannot take any action. | Single tenant | `"Viewer"` |

> **⚠️ Role Name Canonical Mapping**  
> The JWT `roles` claim values above are the exact strings used in both:  
> - Server-side JWT issuance (auth layer)  
> - Client-side `<AuthorizeView Roles="...">` checks (UI layer)  
>
> `Auditor` is **not** a valid role name in this system. The correct claim string for the review role is `Reviewer`.  
> Use these exact strings — case-sensitive. A mismatch between JWT issuance and UI role strings causes silent access failures.

---

## Section 2: Screen Access Matrix

`✅` = allowed · `❌` = denied · `→` = redirected to

| Screen | SuperAdmin | TenantAdmin | Reviewer | Viewer | Unauthenticated |
|---|:---:|:---:|:---:|:---:|:---:|
| **Login** `/login` | → `/audits` | → `/audits` | → `/audits` | → `/audits` | ✅ |
| **Tenant Onboarding** `/onboard` | ✅ | ✅ | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/login` |
| **Audit List** `/audits` | ✅ | ✅ | ✅ | ✅ | ❌ → `/login` |
| **Audit Detail Dashboard** `/audits/{auditRunId}` | ✅ | ✅ | ✅ | ✅ | ❌ → `/login` |
| **Skill Chain Runner** `/audits/{auditRunId}/run` | ✅ | ✅ | ❌ → `/audits/{auditRunId}` | ❌ → `/audits/{auditRunId}` | ❌ → `/login` |
| **Reviewer Queue** `/review` | ✅ | ✅ | ✅ | ❌ → `/audits` | ❌ → `/login` |
| **Calibration Dashboard** `/calibration` | ✅ | ✅ | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/login` |
| **Telemetry Dashboard** `/telemetry` | ✅ | ✅ | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/login` |
| **Client Management** `/clients` | ✅ | ✅ | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/login` |
| **Document Management** `/documents` | ✅ | ✅ | ✅ | ❌ → `/audits` | ❌ → `/login` |
| **Tenant Admin Panel** `/admin/tenant` | ✅ | ✅ | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/login` |
| **Super Admin Panel** `/admin/super` | ✅ | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/audits` | ❌ → `/login` |

### Access Rules

- All authenticated routes default-redirect unauthenticated users to `/login`.
- Authenticated users who land on `/login` are redirected to `/audits`.
- Denied users (authenticated but wrong role) are redirected to the most appropriate permitted screen for their role, **not** to a generic 403 page. This avoids exposing information about the existence of restricted screens to unauthorized users.
- SuperAdmin is additive — they can access every screen a TenantAdmin can, plus Super Admin Panel and cross-tenant data.

---

## Section 3: Action-Level Visibility

These are UI actions that appear or disappear based on role **within** a screen. All use `<AuthorizeView>` in Razor — not code-behind checks.

### Audit Actions

| Action | SuperAdmin | TenantAdmin | Reviewer | Viewer |
|---|:---:|:---:|:---:|:---:|
| Create new audit | ✅ | ✅ | ❌ | ❌ |
| Delete audit | ✅ | ✅ | ❌ | ❌ |
| Start skill chain run | ✅ | ✅ | ❌ | ❌ |
| Publish audit | ✅ | ✅ | ❌ | ❌ |
| View audit detail | ✅ | ✅ | ✅ | ✅ |
| Download published report | ✅ | ✅ | ✅ | ✅ |

### Reviewer Actions (within Category Review Drawer)

| Action | SuperAdmin | TenantAdmin | Reviewer | Viewer |
|---|:---:|:---:|:---:|:---:|
| Open Category Review Drawer | ✅ | ✅ | ✅ | ❌ |
| Approve category result | ✅ | ✅ | ✅ | ❌ |
| Edit (override) score/strategy | ✅ | ✅ | ✅ | ❌ |
| Rerun skill for category | ✅ | ✅ | ✅ | ❌ |
| Escalate to AI Council | ✅ | ✅ | ✅ | ❌ |
| View category evidence (read-only) | ✅ | ✅ | ✅ | ✅ |

### Client & Document Management

| Action | SuperAdmin | TenantAdmin | Reviewer | Viewer |
|---|:---:|:---:|:---:|:---:|
| Create new client | ✅ | ✅ | ❌ | ❌ |
| Edit client details | ✅ | ✅ | ❌ | ❌ |
| Delete client | ✅ | ✅ | ❌ | ❌ |
| Upload documents | ✅ | ✅ | ✅ | ❌ |
| Delete documents | ✅ | ✅ | ❌ | ❌ |
| View document list | ✅ | ✅ | ✅ | ❌ |

### Calibration & Telemetry

| Action | SuperAdmin | TenantAdmin | Reviewer | Viewer |
|---|:---:|:---:|:---:|:---:|
| View calibration history | ✅ | ✅ | ❌ | ❌ |
| View telemetry dashboard | ✅ | ✅ | ❌ | ❌ |
| View cross-tenant telemetry | ✅ | ❌ | ❌ | ❌ |
| Export calibration data | ✅ | ✅ | ❌ | ❌ |

### Tenant & User Management

| Action | SuperAdmin | TenantAdmin | Reviewer | Viewer |
|---|:---:|:---:|:---:|:---:|
| Manage tenant settings | ✅ | ✅ | ❌ | ❌ |
| Invite tenant users | ✅ | ✅ | ❌ | ❌ |
| Change user roles | ✅ | ✅ | ❌ | ❌ |
| Remove tenant users | ✅ | ✅ | ❌ | ❌ |
| Manage tenant onboarding | ✅ | ✅ | ❌ | ❌ |

### Super Admin Actions (Super Admin Panel only)

| Action | SuperAdmin | TenantAdmin | Reviewer | Viewer |
|---|:---:|:---:|:---:|:---:|
| View all tenants | ✅ | ❌ | ❌ | ❌ |
| Suspend / reinstate tenant | ✅ | ❌ | ❌ | ❌ |
| Cross-tenant queries | ✅ | ❌ | ❌ | ❌ |
| View system health | ✅ | ❌ | ❌ | ❌ |
| View platform-wide telemetry | ✅ | ❌ | ❌ | ❌ |

---

## Section 4: Razor Implementation Pattern

### Standard Pattern: `<AuthorizeView>`

All role-gated UI elements use `<AuthorizeView>` with the exact role strings from Section 1. Never use ad-hoc checks in code-behind.

```razor
<!-- Single role -->
<AuthorizeView Roles="SuperAdmin">
    <Authorized>
        <button class="btn-danger">Suspend Tenant</button>
    </Authorized>
</AuthorizeView>

<!-- Multiple roles (comma-separated — OR logic) -->
<AuthorizeView Roles="TenantAdmin,SuperAdmin">
    <Authorized>
        <button class="btn-primary">Start Audit</button>
    </Authorized>
</AuthorizeView>

<!-- With fallback for unauthorized users -->
<AuthorizeView Roles="Reviewer,TenantAdmin,SuperAdmin">
    <Authorized>
        <button class="btn-secondary" @onclick="OpenDrawer">Review</button>
    </Authorized>
    <NotAuthorized>
        <span class="text-muted">View only</span>
    </NotAuthorized>
</AuthorizeView>
```

### Page-Level Authorization

Route-level authorization uses `[Authorize]` attribute on the `@page` component. Custom redirect logic for denied users is handled in `OnInitializedAsync`:

```razor
@attribute [Authorize(Roles = "TenantAdmin,SuperAdmin")]

@code {
    [CascadingParameter] private Task<AuthenticationState> AuthState { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState;
        if (!auth.User.Identity?.IsAuthenticated ?? true)
        {
            NavigationManager.NavigateTo("/login", forceLoad: false);
            return;
        }
        if (!auth.User.IsInRole("TenantAdmin") && !auth.User.IsInRole("SuperAdmin"))
        {
            NavigationManager.NavigateTo("/audits", forceLoad: false);
            return;
        }
        // ... load page data
    }
}
```

### Role String → JWT Claim Mapping

| UI Role String (Razor) | JWT `roles` Claim Value | ASP.NET Core Policy Name |
|---|---|---|
| `"SuperAdmin"` | `"SuperAdmin"` | `"RequireSuperAdmin"` |
| `"TenantAdmin"` | `"TenantAdmin"` | `"RequireTenantAdmin"` |
| `"Reviewer"` | `"Reviewer"` | `"RequireReviewer"` |
| `"Viewer"` | `"Viewer"` | `"RequireViewer"` |

The JWT `roles` claim is an array of strings. `IsInRole()` matches against each element.  
**Critical:** Role names are case-sensitive in both JWT claims and Razor `<AuthorizeView Roles="...">` attributes. The values above are the single source of truth — auth layer (JWT issuance) and UI layer (AuthorizeView) must use identical strings.

### What Never to Do

```razor
<!-- ❌ WRONG — inline code-behind role check, not traceable to this matrix -->
@if (_currentUser.IsInRole("TenantAdmin") || someLocalFlag)
{
    <button>Start Audit</button>
}

<!-- ✅ RIGHT — declarative, traceable to matrix -->
<AuthorizeView Roles="TenantAdmin,SuperAdmin">
    <Authorized>
        <button class="btn-primary">Start Audit</button>
    </Authorized>
</AuthorizeView>
```

Exception: Page-level redirect logic in `OnInitializedAsync` (shown above) is the one approved location for code-behind role checks, and only for navigation decisions, not UI rendering.
