# Auth Spec Reference

> **Status:** Specification  
> **Author:** Smithers (Cloud DevOps)  
> **Date:** 2026-05-10  
> **Audience:** Backend engineer implementing authentication for StrategicGlue Six-to-Fix  
> **Related:** [Section 04 — Infrastructure](prd/sections/04-infrastructure.md) | [Infra Spec](infra-spec.md)

---

## 1. Auth Provider Recommendation

### ✅ Recommendation: Duende IdentityServer

After evaluating all four candidate options against the requirements of a multi-tenant .NET SaaS platform running Blazor Server, **Duende IdentityServer** is the recommended auth provider.

**Why Duende IdentityServer:**

- **Native .NET integration.** IdentityServer runs as an ASP.NET Core middleware project — the same runtime, the same DI container, the same hosting model as the rest of the application. There is no protocol translation layer, no Java interop, and no Azure-proprietary policy language to learn.
- **Full OIDC server.** Duende ships a complete OAuth 2.0/OIDC authorization server, including token issuance, token introspection, refresh tokens, and discovery documents. It integrates directly with `Microsoft.AspNetCore.Authentication.JwtBearer` on the resource side — which is exactly what Blazor Server needs.
- **Multi-tenancy is a first-class design concern.** The token pipeline (profile service, claims transformation) can inject `tenant_id` and `tenant_slug` into every issued token. Tenant isolation is enforced at token issuance, not guessed at in middleware.
- **SAML/OIDC federation per tenant is supported.** Duende IdentityServer supports external identity providers per-client configuration. Adding a per-tenant OIDC or SAML upstream IdP (e.g., when a customer's enterprise SSO needs to be federated) requires configuration changes, not architecture changes.
- **Self-hosted and Azure-compatible.** Hosted on the same App Service as the application (or a companion App Service in a future hardening pass), it does not require an external SaaS vendor. Data sovereignty and tenant PII stay within the Azure subscription.

**Why the alternatives were not chosen:**

| Option | Reason Rejected |
|--------|----------------|
| **ASP.NET Core Identity + JWT** | Not an OIDC server. No discovery endpoint, no per-tenant SSO federation path, requires building refresh token logic from scratch. Appropriate for a simple single-tenant app; insufficient for multi-tenant SaaS with future enterprise SSO. |
| **Azure AD B2C** | Multi-tenant SaaS isolation (one tenant per B2C user flow or directory) requires complex custom policy (IEF XML), which is brittle and hard to maintain. B2C is designed for consumer identity, not B2B SaaS tenant isolation. Future SSO federation requires B2C custom domains and per-tenant policy configuration that becomes operationally expensive at scale. |
| **Keycloak** | Java-based. Requires a separate JVM runtime on Azure (App Service for Java or container). Operational overhead of maintaining two separate runtimes (.NET + JVM) is unjustified when a .NET-native equivalent exists. Keycloak's .NET SDK support is community-driven, not first-party. |

**Licensing note:** Duende IdentityServer requires a commercial license for production deployments above the free community tier thresholds. For a SaaS platform, budget for the **Business** license tier. This cost is justified given the operational savings over self-built token issuance or Azure B2C custom policy maintenance.

---

## 2. Authentication Flows

### 2.1 User Login (Username + Password)

```
1. User navigates to /login in the Blazor Server app
2. Blazor Server renders the login form (server-side; no redirect to external page for basic auth)
3. User submits credentials
4. App sends credentials to IdentityServer token endpoint (POST /connect/token, grant_type=password)
   [Or: redirect to IdentityServer authorization endpoint for authorization_code + PKCE flow]
5. IdentityServer validates credentials against ASP.NET Core Identity user store (PostgreSQL-backed)
6. IdentityServer issues access token (15 min) + refresh token (7 days)
7. App stores tokens in server-side session (never in browser cookies or localStorage)
8. Blazor AuthenticationStateProvider updates — UI re-renders with authenticated state
9. Tenant context is extracted from token claims and injected into scoped ITenantContext service
```

### 2.2 Forgot Password / Reset

```
1. User clicks "Forgot Password" on the login form
2. App renders the password reset request form
3. User submits their email address
4. App looks up the user in the Identity store by email
5. If found: generate a time-limited reset token (via UserManager.GeneratePasswordResetTokenAsync)
6. Send reset email via Azure Communication Services with a link containing the token
   Link format: /reset-password?token={urlEncoded_token}&userId={userId}
7. User clicks link → Blazor Server validates the token via UserManager.ResetPasswordAsync
8. User sets a new password; redirect to login
9. All existing refresh tokens for the user are revoked (IdentityServer persisted grants cleanup)
```

### 2.3 New User Invitation (Tenant Admin Invites a User)

```
1. Tenant Admin navigates to Users → Invite User in the UI
2. Admin provides: email, full name, role (Auditor or Reviewer)
3. App creates an inactive user record in the Identity store with the correct tenant_id
4. App assigns the specified role via UserManager + RoleManager
5. App generates an invitation token (time-limited, 72 hours)
6. Invitation email sent via Azure Communication Services with link:
   /accept-invite?token={token}&userId={userId}
7. Invited user clicks link → prompted to set a password
8. On password set: account activated; user is redirected to login
9. Login flow proceeds normally (§2.1)
```

### 2.4 First-Time Setup (New Tenant Onboarding)

```
1. Super Admin creates a new Tenant record in the platform database
   (tenant_id, tenant_slug, display name, plan tier)
2. Super Admin creates the Tenant Admin user account, assigned to the new tenant
3. Invitation email sent to Tenant Admin (§2.3 flow)
4. Tenant Admin accepts invite, sets password, logs in
5. On first login, Tenant Admin is prompted to complete tenant profile:
   - Billing contact
   - Tenant display name
   - Upload logo (optional)
6. Tenant Admin can then invite Auditors and Reviewers (§2.3 flow)
```

### 2.5 Token Refresh

```
1. Access token expires after 15 minutes (enforced by IdentityServer exp claim)
2. On an API call returning 401: the application detects token expiry
3. App calls IdentityServer token endpoint (POST /connect/token, grant_type=refresh_token)
   using the stored refresh token
4. IdentityServer validates the refresh token, issues a new access token + rotated refresh token
5. App updates the server-side session with the new tokens
6. Original API call is retried with the new access token
7. If refresh token is expired (> 7 days) or revoked: user is redirected to login
```

### 2.6 Logout

```
1. User clicks Logout in the UI
2. App calls IdentityServer end session endpoint (POST /connect/endsession)
3. IdentityServer revokes all refresh tokens for the session
4. App clears the server-side session state
5. Blazor AuthenticationStateProvider sets state to anonymous
6. User is redirected to /login
7. (No browser cookie clearing needed — tokens never touched the browser)
```

---

## 3. JWT Token Structure

Every access token issued by IdentityServer contains the following claims:

```json
{
  "sub": "3f2a1b4c-5d6e-7f8a-9b0c-1d2e3f4a5b6c",
  "email": "user@example.com",
  "name": "Jane Doe",
  "tenant_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tenant_slug": "acme-marketing",
  "roles": ["Auditor"],
  "exp": 1747868400,
  "iss": "https://app-strategicglue-prod.azurewebsites.net",
  "aud": "strategicglue-api",
  "iat": 1747867500,
  "jti": "unique-token-id"
}
```

### Claim Descriptions

| Claim | Type | Description | Why Required |
|-------|------|-------------|-------------|
| `sub` | UUID string | Stable, unique user identifier. Never reused. | Primary key for user identity across all services |
| `email` | string | User's email address | Display, notifications, and user lookup. **Not logged in structured logs.** |
| `name` | string | User's full display name | UI display only |
| `tenant_id` | UUID string | Tenant the user belongs to | All database queries and Blob paths are scoped by this value |
| `tenant_slug` | string | URL-safe slug for the tenant (e.g., `acme-marketing`) | URL construction, Blob container prefixes, display |
| `roles` | string array | One or more RBAC roles (see §4) | Authorization policy enforcement |
| `exp` | Unix timestamp | Token expiry (15 min from issuance) | Token validity enforcement |
| `iss` | URI | IdentityServer issuer URL | Token authenticity verification |
| `aud` | string | Intended audience (`strategicglue-api`) | Prevents token reuse across services |
| `iat` | Unix timestamp | Issued at time | Audit trail |
| `jti` | UUID string | Unique token ID | Replay attack prevention, token revocation index |

**Security notes:**
- `tenant_id` is set by the IdentityServer profile service from the user record's stored tenant — it is not a claim the client can request or override.
- `roles` are assigned at the user level in the Identity store; a user cannot escalate their own roles.
- No client data, audit content, or PII beyond email and name is placed in the JWT.

---

## 4. RBAC Matrix

### Role Definitions

| Role | Scope | Description |
|------|-------|-------------|
| **Super Admin** | Platform-wide | StrategicGlue staff. Can manage all tenants and all users across the platform. |
| **Tenant Admin** | Within their tenant | Can manage users, clients, and configuration within their tenant. Cannot see other tenants. |
| **Auditor** | Within their tenant | Creates clients, uploads documents, creates and runs audits, reviews results. |
| **Reviewer** | Within their tenant | Views the reviewer queue, approves/edits/rejects/escalates category results. |

### Permission Matrix

| Action | Super Admin | Tenant Admin | Auditor | Reviewer |
|--------|:-----------:|:------------:|:-------:|:--------:|
| **Tenant Management** | | | | |
| Create tenant | ✅ | ❌ | ❌ | ❌ |
| Edit tenant settings | ✅ | ✅ (own) | ❌ | ❌ |
| Delete tenant | ✅ | ❌ | ❌ | ❌ |
| View all tenants | ✅ | ❌ | ❌ | ❌ |
| **User Management** | | | | |
| Invite users | ✅ | ✅ (own tenant) | ❌ | ❌ |
| Edit user roles | ✅ | ✅ (own tenant) | ❌ | ❌ |
| Deactivate users | ✅ | ✅ (own tenant) | ❌ | ❌ |
| View user list | ✅ | ✅ (own tenant) | ❌ | ❌ |
| **Client Management** | | | | |
| Create clients | ✅ | ✅ | ✅ | ❌ |
| Edit client details | ✅ | ✅ | ✅ | ❌ |
| Delete clients | ✅ | ✅ | ❌ | ❌ |
| View clients | ✅ | ✅ | ✅ | ✅ (assigned) |
| **Document Management** | | | | |
| Upload documents | ✅ | ✅ | ✅ | ❌ |
| Delete documents | ✅ | ✅ | ✅ (own) | ❌ |
| View documents | ✅ | ✅ | ✅ | ✅ (assigned) |
| **Audit Management** | | | | |
| Create audits | ✅ | ✅ | ✅ | ❌ |
| Run skills (trigger AI) | ✅ | ✅ | ✅ | ❌ |
| Rerun individual categories | ✅ | ✅ | ✅ | ❌ |
| Delete audits | ✅ | ✅ | ❌ | ❌ |
| **Reviewer Queue** | | | | |
| View reviewer queue | ✅ | ✅ | ❌ | ✅ |
| Approve category result | ✅ | ✅ | ❌ | ✅ |
| Edit category result | ✅ | ✅ | ❌ | ✅ |
| Reject / request rerun | ✅ | ✅ | ❌ | ✅ |
| Escalate category | ✅ | ✅ | ❌ | ✅ |
| Publish audit | ✅ | ✅ | ❌ | ❌ |
| **Calibration & Telemetry** | | | | |
| View calibration data | ✅ | ✅ | ❌ | ❌ |
| View telemetry / run metrics | ✅ | ✅ | ❌ | ❌ |
| Configure skill policies | ✅ | ❌ | ❌ | ❌ |
| View skill policies | ✅ | ✅ | ❌ | ❌ |

---

## 5. ASP.NET Core Integration

### 5.1 Program.cs Configuration (Pseudocode)

```csharp
// ── Authentication ─────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IDENTITYSERVER__ISSUER"];
        options.Audience  = "strategicglue-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
    });

// ── Authorization Policies ─────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    // Role-based convenience policies
    options.AddPolicy("SuperAdmin",    p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("TenantAdmin",   p => p.RequireRole("SuperAdmin", "TenantAdmin"));
    options.AddPolicy("Auditor",       p => p.RequireRole("SuperAdmin", "TenantAdmin", "Auditor"));
    options.AddPolicy("Reviewer",      p => p.RequireRole("SuperAdmin", "TenantAdmin", "Reviewer"));

    // Any authenticated user within a tenant
    options.AddPolicy("AnyTenantUser", p =>
    {
        p.RequireAuthenticatedUser();
        p.RequireClaim("tenant_id");
    });

    // Default policy for all authenticated endpoints
    options.DefaultPolicy = options.GetPolicy("AnyTenantUser")!;
    options.FallbackPolicy = options.DefaultPolicy;
});

// ── Tenant Context ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();

// ── Blazor ─────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// ── Middleware Pipeline ────────────────────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

### 5.2 Injecting Tenant Context into Blazor Components

Define `ITenantContext` as a scoped service resolved from JWT claims:

```csharp
public interface ITenantContext
{
    Guid   TenantId   { get; }
    string TenantSlug { get; }
    string UserId     { get; }
    bool   IsResolved { get; }
}

public sealed class ClaimsTenantContext : ITenantContext
{
    public ClaimsTenantContext(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User;
        IsResolved  = user?.Identity?.IsAuthenticated ?? false;
        TenantId    = Guid.TryParse(user?.FindFirst("tenant_id")?.Value, out var tid) ? tid : Guid.Empty;
        TenantSlug  = user?.FindFirst("tenant_slug")?.Value ?? string.Empty;
        UserId      = user?.FindFirst("sub")?.Value ?? string.Empty;
    }

    public Guid   TenantId   { get; }
    public string TenantSlug { get; }
    public string UserId     { get; }
    public bool   IsResolved { get; }
}
```

In a Blazor component:

```razor
@inject ITenantContext Tenant

<h2>@Tenant.TenantSlug Dashboard</h2>
```

### 5.3 Role-Based Authorization in Blazor

**Component-level (declarative):**
```razor
@attribute [Authorize(Roles = "Reviewer,TenantAdmin,SuperAdmin")]

<ReviewerQueue />
```

**Inline conditional rendering:**
```razor
<AuthorizeView Roles="TenantAdmin,SuperAdmin">
    <Authorized>
        <button @onclick="InviteUser">Invite User</button>
    </Authorized>
</AuthorizeView>
```

**Policy-based (in API endpoints or service layer):**
```csharp
[Authorize(Policy = "TenantAdmin")]
public IActionResult InviteUser([FromBody] InviteUserRequest req) { ... }
```

### 5.4 Reviewer Lockout and HTTP 409

Reviewer lockout is an **application-layer concern**, not an authentication/authorization concern. When a Reviewer opens a category for editing, a lock record is created in the database (`reviewer_locks` table) with an expiry. If a second Reviewer attempts to open the same category:

- The application checks the lock table before serving the reviewer view.
- If a valid (non-expired) lock exists for a different user: return **HTTP 409 Conflict** with a body describing who holds the lock and when it expires.
- **Do not return 401 (Unauthorized) or 403 (Forbidden)** — the Reviewer is authenticated and authorized; they are simply locked out of this specific resource instance.

The lock is released when:
1. The locking Reviewer submits or explicitly cancels their edit.
2. The lock TTL expires (default: 15 minutes — matches access token expiry).
3. A Super Admin or Tenant Admin forcibly releases it via the admin UI.

---

## 6. Security Considerations

### Token Expiry

| Token Type | Lifetime | Notes |
|------------|----------|-------|
| Access token | 15 minutes | Short-lived; limits blast radius of a stolen token |
| Refresh token | 7 days | Sliding expiry — activity resets the clock |
| Invitation token | 72 hours | Single-use; invalidated on acceptance |
| Password reset token | 1 hour | Single-use; invalidated on use |

### Token Storage in Blazor Server

Access and refresh tokens are stored in the **server-side ASP.NET Core session** (backed by distributed cache, e.g., Redis for prod or in-memory for dev). Tokens are never:
- Written to a browser cookie (beyond a session identifier cookie for circuit binding)
- Written to `localStorage` or `sessionStorage`
- Embedded in rendered HTML

This architecture eliminates XSS-based token theft and is the primary security advantage of Blazor Server over SPA patterns.

### PII Handling

- **No PII in structured logs.** Log only `user_id` (UUID), `tenant_id` (UUID), and category names. Never log email addresses, client names, or audit content values.
- **No PII in JWT beyond email and name.** Do not place client data, audit scores, or contact details in JWT claims.
- **Email is a claim** — it is acceptable in JWT for display/notification purposes, but must not appear in log statements.

### CSRF

Blazor Server's interactive UI operates over a persistent SignalR WebSocket. CSRF is **not applicable** to the Blazor Server UI — there are no form POSTs that cross the origin boundary.

However, any traditional REST API endpoints (e.g., webhook receivers, external integrations) that process browser-originated requests **must** use ASP.NET Core's anti-forgery middleware:

```csharp
app.UseAntiforgery();
```

Mark Blazor component forms with `<AntiforgeryToken />` when they submit to traditional endpoints outside the Blazor circuit.

### Rate Limiting — Failed Login Attempts

ASP.NET Core Identity's built-in lockout is configured:

| Setting | Value |
|---------|-------|
| Max failed attempts | 5 |
| Lockout duration | 15 minutes |
| Count only authenticated failures | Yes |

```csharp
builder.Services.Configure<LockoutOptions>(options =>
{
    options.AllowedForNewUsers     = true;
    options.MaxFailedAccessAttempts = 5;
    options.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
});
```

After lockout, the user receives a generic error message ("Too many failed attempts — please try again later") without revealing whether the lockout is due to the username or password being correct. Super Admins can unlock accounts manually via the admin UI.

### Additional Security Headers

Configure the following security headers on the App Service or in the ASP.NET Core middleware:

| Header | Value |
|--------|-------|
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `default-src 'self'; connect-src 'self' wss: (IdentityServer URL)` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |

The `Content-Security-Policy` must allow `wss:` for Blazor Server's SignalR connection and must include the IdentityServer origin if it is hosted separately.
