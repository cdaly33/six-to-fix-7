# Trinity 9 headless login diagnosis

- Prod URL: https://app-sixtofix-prod.azurewebsites.net
- Started: 2026-05-20T01:09:51.742Z
- Result: **BLOCKED**

## Source contract

- src/SixToFix.Web/Pages/Login.razor: EditForm invokes JS SixToFix.login(email,password); no hidden antiforgery fields in source.
- src/SixToFix.Web/wwwroot/js/auth.js: fetch POST /api/auth/login JSON with credentials:same-origin.
- src/SixToFix.Api/Endpoints/ApiEndpointExtensions.cs: /api/auth/login accepts LoginRequest JSON, SignInAsync cookie on success.

## Login DOM

Hidden inputs captured on /login: `[ { "name": "__RequestVerificationToken", "id": "", "value": "<redacted-antiforgery-token>" } ]`. The rendered Blazor form includes #email, #password, a submit button, and a generated antiforgery hidden input/cookie.

## Attempts

### Attempt 1: Playwright page.fill then submit click

- Logged in: no
- /api/auth/login response status: 401
- Final URL: https://app-sixtofix-prod.azurewebsites.net/login
- Cookies: [
  {
    "name": ".AspNetCore.Antiforgery.RtGCWVXC8-4",
    "domain": "app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": false,
    "sameSite": "Strict"
  },
  {
    "name": "ARRAffinity",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "Lax"
  },
  {
    "name": "ARRAffinitySameSite",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "None"
  }
]
- Captured request(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "method": "POST",
    "headers": {
      "sec-ch-ua-platform": "\"Windows\"",
      "referer": "https://app-sixtofix-prod.azurewebsites.net/login",
      "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/148.0.7778.96 Safari/537.36",
      "sec-ch-ua": "\"Chromium\";v=\"148\", \"HeadlessChrome\";v=\"148\", \"Not/A)Brand\";v=\"99\"",
      "content-type": "application/json",
      "sec-ch-ua-mobile": "?0"
    },
    "postData": "{\"email\":\"<redacted-email>\",\"password\":\"<redacted>\"}"
  }
]
```
- Captured response(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "status": 401,
    "headers": {
      "strict-transport-security": "max-age=2592000",
      "content-security-policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; img-src 'self' data: blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
      "x-correlation-id": "b7308d5b-f8a6-44c9-82ab-09684c135c26",
      "x-content-type-options": "nosniff",
      "referrer-policy": "strict-origin-when-cross-origin",
      "date": "Wed, 20 May 2026 01:09:56 GMT",
      "content-type": "application/problem+json",
      "server": "Kestrel",
      "x-frame-options": "DENY"
    },
    "body": "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"title\":\"Unauthorized\",\"status\":401,\"detail\":\"Invalid credentials.\"}"
  }
]
```

### Attempt 2: fresh context, click/type, Tab blur, Enter submit

- Logged in: no
- /api/auth/login response status: 401
- Final URL: https://app-sixtofix-prod.azurewebsites.net/login
- Cookies: [
  {
    "name": ".AspNetCore.Antiforgery.RtGCWVXC8-4",
    "domain": "app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": false,
    "sameSite": "Strict"
  },
  {
    "name": "ARRAffinity",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "Lax"
  },
  {
    "name": "ARRAffinitySameSite",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "None"
  }
]
- Captured request(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "method": "POST",
    "headers": {
      "sec-ch-ua-platform": "\"Windows\"",
      "referer": "https://app-sixtofix-prod.azurewebsites.net/login",
      "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/148.0.7778.96 Safari/537.36",
      "sec-ch-ua": "\"Chromium\";v=\"148\", \"HeadlessChrome\";v=\"148\", \"Not/A)Brand\";v=\"99\"",
      "content-type": "application/json",
      "sec-ch-ua-mobile": "?0"
    },
    "postData": "{\"email\":\"<redacted-email>\",\"password\":\"<redacted>\"}"
  }
]
```
- Captured response(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "status": 401,
    "headers": {
      "strict-transport-security": "max-age=2592000",
      "content-security-policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; img-src 'self' data: blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
      "x-correlation-id": "6750e50f-71ff-4122-aa3e-ba49e0dcc196",
      "x-content-type-options": "nosniff",
      "referrer-policy": "strict-origin-when-cross-origin",
      "date": "Wed, 20 May 2026 01:10:02 GMT",
      "content-type": "application/problem+json",
      "server": "Kestrel",
      "x-frame-options": "DENY"
    },
    "body": "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"title\":\"Unauthorized\",\"status\":401,\"detail\":\"Invalid credentials.\"}"
  }
]
```

### Attempt 3: call window.SixToFix.login after Blazor script load

- Logged in: no
- JS result: unauthorized
- Final URL: https://app-sixtofix-prod.azurewebsites.net/login
- Cookies: [
  {
    "name": ".AspNetCore.Antiforgery.RtGCWVXC8-4",
    "domain": "app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": false,
    "sameSite": "Strict"
  },
  {
    "name": "ARRAffinity",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "Lax"
  },
  {
    "name": "ARRAffinitySameSite",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "None"
  }
]
- Captured request(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "method": "POST",
    "headers": {
      "sec-ch-ua-platform": "\"Windows\"",
      "referer": "https://app-sixtofix-prod.azurewebsites.net/login",
      "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/148.0.7778.96 Safari/537.36",
      "sec-ch-ua": "\"Chromium\";v=\"148\", \"HeadlessChrome\";v=\"148\", \"Not/A)Brand\";v=\"99\"",
      "content-type": "application/json",
      "sec-ch-ua-mobile": "?0"
    },
    "postData": "{\"email\":\"<redacted-email>\",\"password\":\"<redacted>\"}"
  }
]
```
- Captured response(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "status": 401,
    "headers": {
      "strict-transport-security": "max-age=2592000",
      "content-security-policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; img-src 'self' data: blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
      "x-correlation-id": "f77eaf43-86e3-4288-aa5a-fff97d2e90cd",
      "x-content-type-options": "nosniff",
      "referrer-policy": "strict-origin-when-cross-origin",
      "date": "Wed, 20 May 2026 01:10:06 GMT",
      "content-type": "application/problem+json",
      "server": "Kestrel",
      "x-frame-options": "DENY"
    },
    "body": "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"title\":\"Unauthorized\",\"status\":401,\"detail\":\"Invalid credentials.\"}"
  }
]
```

### Attempt 4: browser fetch JSON directly from login origin

- Logged in: no
- Direct fetch result: 401 not OK
- Final URL: https://app-sixtofix-prod.azurewebsites.net/login
- Cookies: [
  {
    "name": ".AspNetCore.Antiforgery.RtGCWVXC8-4",
    "domain": "app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": false,
    "sameSite": "Strict"
  },
  {
    "name": "ARRAffinity",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "Lax"
  },
  {
    "name": "ARRAffinitySameSite",
    "domain": ".app-sixtofix-prod.azurewebsites.net",
    "path": "/",
    "expires": -1,
    "httpOnly": true,
    "secure": true,
    "sameSite": "None"
  }
]
- Captured request(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "method": "POST",
    "headers": {
      "sec-ch-ua-platform": "\"Windows\"",
      "referer": "https://app-sixtofix-prod.azurewebsites.net/login",
      "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/148.0.7778.96 Safari/537.36",
      "sec-ch-ua": "\"Chromium\";v=\"148\", \"HeadlessChrome\";v=\"148\", \"Not/A)Brand\";v=\"99\"",
      "content-type": "application/json",
      "sec-ch-ua-mobile": "?0"
    },
    "postData": "{\"email\":\"<redacted-email>\",\"password\":\"<redacted>\"}"
  }
]
```
- Captured response(s):

```json
[
  {
    "url": "https://app-sixtofix-prod.azurewebsites.net/api/auth/login",
    "status": 401,
    "headers": {
      "strict-transport-security": "max-age=2592000",
      "content-security-policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; img-src 'self' data: blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
      "x-correlation-id": "a0409d63-a583-434a-b4b9-a856bfb37475",
      "x-content-type-options": "nosniff",
      "referrer-policy": "strict-origin-when-cross-origin",
      "date": "Wed, 20 May 2026 01:10:09 GMT",
      "content-type": "application/problem+json",
      "server": "Kestrel",
      "x-frame-options": "DENY"
    },
    "body": "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"title\":\"Unauthorized\",\"status\":401,\"detail\":\"Invalid credentials.\"}"
  }
]
```
- Direct fetch body/headers:

```json
{
  "status": 401,
  "ok": false,
  "headers": {
    "content-security-policy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' https://fonts.googleapis.com 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com data:; connect-src 'self' wss: https://fonts.googleapis.com https://fonts.gstatic.com; img-src 'self' data: blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'",
    "content-type": "application/problem+json",
    "date": "Wed, 20 May 2026 01:10:09 GMT",
    "referrer-policy": "strict-origin-when-cross-origin",
    "server": "Kestrel",
    "strict-transport-security": "max-age=2592000",
    "x-content-type-options": "nosniff",
    "x-correlation-id": "a0409d63-a583-434a-b4b9-a856bfb37475",
    "x-frame-options": "DENY"
  },
  "body": "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"title\":\"Unauthorized\",\"status\":401,\"detail\":\"Invalid credentials.\"}"
}
```

## Public fallback checks

Because authenticated login remained blocked, authenticated Clients/Admin/Dashboard checks are **UNVERIFIED**. Public fallback captures:

- `/` returned HTTP 200 and rendered the public home page: `home-public.png`.
- `/templates` returned HTTP 200 but redirected to `/login?returnUrl=%2Ftemplates`: `templates-public.png`. This means public template rendering is **FAIL/UNVERIFIED** without a successful login, despite the expected public behavior.
- `/audit-runs` returned HTTP 404 with an empty body, not a 500: `audit-runs-public.png`.

## Screenshots

- login-dom-before-submit.png
- login-filled-before-submit.png
- login-attempt1-after-submit.png
- login-attempt2-after-submit.png
- login-attempt3-after-js.png
- login-attempt4-after-fetch.png
- home-public.png
- templates-public.png
- audit-runs-public.png

## Root cause assessment

All browser-driven attempts submitted JSON to /api/auth/login with the expected request shape. The login page does render an antiforgery hidden input and antiforgery cookie, but the browser JS contract posts JSON to /api/auth/login and the endpoint does not validate an antiforgery token. The app returned 401 Invalid credentials from the credential check every time, and no sixtofix.auth cookie was issued. This points to a production identity/password/user-state mismatch rather than a Playwright-only missing-token/cookie issue.


