
## Current Security Posture — Full Analysis

### ✅ Already done / Good

| Item | Status |
|------|--------|
| CSRF protection | ✅ Solved — JSON API via `fetch()`, not HTML forms |
| Token expiry | ✅ Fixed — now 24 hours (was 24 years) |
| `SameSite=Strict` on cookie | ✅ Good |
| `MapInboundClaims = false` + `RoleClaimType = "role"` | ✅ Correct claim mapping |
| HTTPS redirection | ✅ Enabled |

### ⚠️ Remaining issues

| # | Issue | Severity | Effort to fix |
|---|-------|----------|--------------|
| 1 | Cookie not `HttpOnly` — token readable by JS | 🔴 HIGH | Low |
| 2 | No `Secure` flag on cookie | 🟡 MEDIUM | Trivial |
| 3 | Logout is client-side only | 🟡 MEDIUM | Medium |
| 4 | Hardcoded JWT secret in `appsettings.json` | 🟡 MEDIUM (dev) / 🔴 HIGH (prod) | Low |
| 5 | User registration is wide open | 🟢 LOW | Low |

---

## Fix options — ranked by effort

### 🔧 Fix 1 (Trivial): `Secure` flag on cookie

**File:** `wwwroot/js/auth.js`

Change this:
```javascript
document.cookie =
  "auth_token=" + data.token + "; path=/; max-age=86400; samesite=strict";
```

To this:
```javascript
document.cookie =
  "auth_token=" + data.token + "; path=/; max-age=86400; samesite=strict" +
  (location.protocol === "https:" ? "; secure" : "");
```

The `location.protocol` check means it works on both HTTP (dev) and HTTPS (production). It's a no-brainer.

---

### 🔧 Fix 2 (Low effort): Move cookie setting to server with `HttpOnly`

**This is the biggest bang-for-buck improvement.** Instead of JS setting the cookie, have the server set it — with `HttpOnly` (not readable by JS) and `Secure`.

**File:** `Controllers/AuthController.cs` — modify the `Login` endpoint:

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var user = await _userManager.FindByEmailAsync(request.Email);
    if (user == null)
        return Unauthorized(new { Message = "Invalid credentials" });

    var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
    if (!result.Succeeded)
        return Unauthorized(new { Message = "Invalid credentials" });

    var token = await GenerateJwtToken(user);

    // 🔒 Set cookie server-side with HttpOnly + Secure
    Response.Cookies.Append("auth_token", token, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        MaxAge = TimeSpan.FromHours(24)
    });

    return Ok(new { Message = "Login successful" });
}
```

**File:** `wwwroot/js/auth.js` — simplify login, it no longer touches cookies:

```javascript
login: async function (email, password) {
    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      if (!response.ok) {
        const errorData = await response.json();
        return { success: false, message: errorData.message || "Invalid credentials" };
      }
      // Cookie set by server — just reload
      window.location.href = "/";
      return { success: true };
    } catch (error) {
      return { success: false, message: "Connection error: " + error.message };
    }
},
```

**Why this matters:**
- `HttpOnly` → even if there's an XSS vulnerability, the attacker **cannot read the token**
- The token is never exposed to JS at any point
- `Secure` → only sent over HTTPS
- Same origin policy prevents CSRF when using `fetch()` + cookies

---

### 🔧 Fix 3 (Medium effort): Move JWT secret to `appsettings.Development.json` / User Secrets

**File:** `appsettings.json` — remove the JWT Key from here:

```json
"Jwt": {
    "Issuer": "BlazorWebAppMovies",
    "Audience": "BlazorWebAppMovies"
}
```

For development, use **User Secrets**:
```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "YourLocalDevKeyThatIsAtLeast32Chars!"
```

For production, use environment variables or Azure Key Vault / AWS Secrets Manager.

**Why:** Having the JWT signing key in `appsettings.json` that gets committed to git is a risk. The current value `"SuperSecretKeyThatIsAtLeast32CharactersLong!"` is obviously a placeholder, but it's still checked into source control.

---

### 🔧 Fix 4 (Medium effort): Server-side logout

**File:** `Controllers/AuthController.cs` — add logout endpoint:

```csharp
[HttpPost("logout")]
[Authorize]
public async Task<IActionResult> Logout()
{
    // For a simple approach: the token expiry handles it (24h max)
    // For a more robust approach: add to a blacklist (in-memory cache, Redis, or DB)
    // For now, returning Ok is fine since the cookie is cleared client-side
    // and the token has a limited lifetime
    return Ok(new { Message = "Logged out" });
}
```

**File:** `wwwroot/js/auth.js`:

```javascript
logout: async function () {
    try {
        await fetch("/api/auth/logout", { method: "POST" });
    } catch (e) { /* ignore */ }
    document.cookie = "auth_token=; path=/; max-age=0; samesite=strict";
    window.location.href = "/";
}
```

Since `HttpOnly` cookies are sent automatically with `fetch()` to the same origin, the JWT will be attached.

---

## 🎯 Recommended action plan

| Step | What | Effort | Security impact |
|------|------|--------|----------------|
| 1 | **Secure flag** on cookie | 1 minute | 🟡 Medium |
| 2 | **Server-side cookie with HttpOnly** | 15 minutes | 🔴 High (fixes XSS exposure) |
| 3 | **Move JWT key out of appsettings.json** | 5 minutes | 🟡 Medium (prevents secret leakage) |
| 4 | **Server-side logout** | 15 minutes | 🟡 Medium (stops cookie reuse after logout) |

Steps 1+2 together solve the biggest remaining problem — the JWT being accessible to JavaScript.

I'd recommend **step 2 (HttpOnly cookie from server)** as the highest priority — it's the most impactful and actually simplifies your `auth.js` file.

--------------------------------------------------------------------------------------

---

# Security Audit — Fixes & Remaining Issues

> **Date:** 2026-07-06
> **Scope:** Authentication (JWT + cookies), Antiforgery, CSRF protection

---

## ✅ Initial Fixes (already applied)

### 🔴 Critical

| # | Issue | Before | After | File(s) |
|---|-------|--------|-------|---------|
| 1 | **JWT token expiry** | `AddYears(24)` — valid for 24 years | `AddHours(24)` — valid for 24 hours | `Controllers/AuthController.cs` |
| 2 | **Claim mapping + role claim** | Default mapping caused role lookup failures | `MapInboundClaims = false`, `RoleClaimType = "role"`, claims use `new Claim("role", role)` | `Program.cs`, `Controllers/AuthController.cs` |
| 3 | **CSRF on login** | HTML form POST with broken `PersistentComponentState` token → silent login failure | Login via `fetch()` JSON API — not CSRF-able (same-origin) | `wwwroot/js/auth.js`, `Components/Pages/Home.razor` |
| 4 | **Antiforgery plumbing removed** | `IAntiforgery` / `IHttpContextAccessor` / `PersistentComponentState` in components | Clean JS-interop without hidden token fields | `Home.razor`, `MainLayout.razor` |

### 🟠 High

| # | Issue | Before | After | File(s) |
|---|-------|--------|-------|---------|
| 5 | **Auto-migration in all environments** | Ran on every startup including production | Wrapped in `if (app.Environment.IsDevelopment())` | `Program.cs` |
| 6 | **MigrationsEndPoint in wrong block** | In non-Development block | Moved to Development block | `Program.cs` |

### 🟡 Medium

| # | Issue | Before | After | File(s) |
|---|-------|--------|-------|---------|
| 7 | **Redundant DbContext registrations** | Multiple factories + `AddScoped` | Cleaned up via `DbContextProvider` pattern | `Program.cs` |
| 8 | **Untyped DbContextOptions** | Used untyped `DbContextOptions` | Typed `DbContextOptions<BlazorWebAppMoviesContext>` | `Data/*.cs` |
| 9 | **Mixed namespace styles** | Mix of file-scoped and block-scoped | All file-scoped | Multiple files |

---

## ❌ Remaining Issues (not yet fixed)

### 🔴 High Priority

| # | Issue | Details | Recommended fix | Effort |
|---|-------|---------|----------------|--------|
| **R1** | **Cookie not HttpOnly** — JWT readable by JavaScript | `auth.js` sets `auth_token` via `document.cookie`. Any XSS (e.g. from movie data) leaks the token. | Server sets cookie via `Response.Cookies.Append()` with `HttpOnly = true` | ~15 min |
| **R2** | **No server-side logout** | Logout only clears cookie client-side. JWT remains valid for 24 hours. | Add `/api/auth/logout` endpoint + optional token blacklist | ~30 min |

### 🟡 Medium Priority

| # | Issue | Details | Recommended fix | Effort |
|---|-------|---------|----------------|--------|
| **R3** | **No Secure flag on cookie** | Cookie sent over HTTP too — not just HTTPS | Add `location.protocol === "https:" ? "; secure" : ""` in JS, or better — let server set it | ~1 min |
| **R4** | **JWT signing key in appsettings.json** | `"SuperSecretKey..."` is hardcoded and checked into git | Use **User Secrets** for dev, env vars / Key Vault for production | ~5 min |
| **R5** | **Register endpoint is open** | Anyone can register — no email confirmation or admin approval | Add email confirmation or restrict to admins | ~1-2h* |

### 🟢 Low Priority

| # | Issue | Details | Recommended fix | Effort |
|---|-------|---------|----------------|--------|
| **R6** | **Login form on Home page** | Auth UI mixed with app content | Move to dedicated `/login` page | ~30 min |
| **R7** | **No rate limiting on auth endpoints** | Brute-force depends only on Identity lockout defaults | Add rate limiting middleware or WAF | ~1h* |

> *\* Estimated effort includes setup and testing*

---

## 🎯 Recommended priority

```
Sprint 1 (now)
  ├── R1 — HttpOnly cookie from server     🔴 Impact: blocks XSS token theft
  ├── R2 — Server-side logout              🔴 Impact: token invalidation
  └── R3 — Secure flag (bundled with R1)   🟡

Sprint 2 (next)
  ├── R4 — JWT key out of appsettings.json 🟡
  └── R5 — Registration controls           🟡

Backlog
  ├── R6 — Dedicated login page            🟢
  └── R7 — Rate limiting                   🟢
```

---

## Security architecture

```
Browser                      ASP.NET Core
┌───────────────┐   fetch POST    ┌───────────────────────┐
│ Home.razor    │ ──────────────→ │ AuthController.Login  │
│ (Blazor)      │                 │  ✓ Validate password  │
│               │ ←────────────── │  ✗ Token in JSON body │
│               │   { token }     │                       │
│               │                 └───────────────────────┘
│ JS sets       │                 ┌───────────────────────┐
│ cookie        │                 │ JwtBearerEvents       │
│ (no HttpOnly) │                 │  ← reads auth_token   │
│               │                 │    from cookie        │
└───────────────┘                 └───────────────────────┘

Current:  Token exposed to JS (no HttpOnly)
Desired:  Token set by server (HttpOnly: true, Secure: true)
```

## Environment notes

| Profile | URL | Secure flag works? |
|---------|-----|-------------------|
| `http` | `http://localhost:5216` | ❌ No — browser ignores Secure on HTTP |
| `https` | `https://localhost:7083` | ✅ Yes |
| Production | HTTPS | ✅ Yes — mandatory |
