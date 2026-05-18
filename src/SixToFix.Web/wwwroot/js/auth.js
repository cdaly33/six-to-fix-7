// Browser-side auth helpers.
// Login.razor calls SixToFix.login() via JS interop so that the browser (not
// the server-side HttpClient) makes the credential POST. The response carries a
// Set-Cookie header that the browser attaches to every subsequent navigation,
// which is what enables Blazor SSR pages to see an authenticated user.
window.SixToFix = window.SixToFix || {};

// Posts credentials to /api/auth/login.
// Returns "ok" on success, "unauthorized" for 401, or "error:<status>" otherwise.
// On success the JWT is persisted to localStorage for /api fetch usage.
window.SixToFix.login = async function (email, password) {
    try {
        const resp = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({ email, password })
        });
        if (!resp.ok) {
            return resp.status === 401 ? 'unauthorized' : `error:${resp.status}`;
        }
        const data = await resp.json();
        if (data.accessToken) {
            localStorage.setItem('six_to_fix_token', data.accessToken);
        }
        return 'ok';
    } catch (e) {
        return `error:${e.message}`;
    }
};

// Removes the stored JWT from localStorage.
// Cookie sign-out is handled server-side by navigating to /logout.
window.SixToFix.clearToken = function () {
    localStorage.removeItem('six_to_fix_token');
};
