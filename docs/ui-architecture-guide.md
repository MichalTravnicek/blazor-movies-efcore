# UI Architecture Guide

> BlazorWebAppMovies — Dual UI Architecture
> Document version: 2026-07-08

---

## Overview

The application provides **two parallel user interfaces** — the **Blazor UI** (Interactive Server) and the **Classic UI** (Razor Pages + jQuery). Both share the same database, API controllers, and authentication system. The user chooses which UI to use at startup, and their preference is stored in a cookie.

---

## Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                      BlazorWebAppMovies                          │
│                                                                  │
│  ┌─────────────────────────────┐  ┌───────────────────────────┐  │
│  │         Blazor UI           │  │       Classic UI          │  │
│  │    (Interactive Server)     │  │ (Razor Pages + jQuery)    │  │
│  │                             │  │                           │  │
│  │  ┌─────────────────────┐    │  │  ┌─────────────────┐      │  │
│  │  │  Movie Pages        │    │  │  │  Page Models    │      │  │
│  │  │  (HttpClient) ──────│────│──│──│  (UserManager)  │      │  │
│  │  └─────────────────────┘    │  │  └────────┬────────┘      │  │
│  │  ┌─────────────────────┐    │  │           │               │  │
│  │  │  User Management    │    │  │  ┌────────┴────────┐      │  │
│  │  │  (UserManager) ─────│────│──│──│  jQuery AJAX    │      │  │
│  │  └─────────────────────┘    │  │  └────────┬────────┘      │  │
│  │  ┌─────────────────────┐    │  └───────────┼───────────────┘  │
│  │  │  Login (JS interop) │    │              │                  │
│  │  │  (/api/auth/*) ─────│────│──────────────┘                  │
│  │  └─────────────────────┘    │                                 │
│  └───────────────│─────────────┘                                 │
│                  │                                               │
│                  ▼                                               │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │                   API Controllers                        │    │
│  │            (Movies, Auth, Admin)                         │    │
│  └───────────────────────────┬──────────────────────────────┘    │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │                    Database (SQLite)                     │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘

**Data flow paths:**

| Path | Description |
|------|-------------|
| Blazor UI → HttpClient → API Controllers → Database | Movie list, CRUD (via `QuickGrid` + API) |
| Blazor UI → UserManager → Database | User management, auth state (bypasses API) |
| Blazor UI → JS interop → API Controllers → Database | Login/logout (`/api/auth/*`) |
| Classic UI → Page Models → UserManager → Database | Users page server-side data |
| Classic UI → jQuery AJAX → API Controllers → Database | Movies CRUD, user CRUD, login |

---

## UI Comparison

### Blazor UI

| Aspect | Detail |
|--------|--------|
| **Render mode** | Interactive Server (SignalR) |
| **Pages** | `.razor` components in `Components/Pages/` |
| **Data access** | Via API: `IHttpClientFactory` + `AuthCookieHandler` → `/api/movies/*` endpoints |
| **Auth** | `[Authorize]` attributes, `AuthenticationState` cascading parameter |
| **Grid** | `QuickGrid` with sorting, pagination, filtering |
| **URLs** | `/movies`, `/movies/create`, `/movies/edit?id=`, `/movies/details?id=`, `/movies/delete?id=`, `/usermanagement` |

### Classic UI

| Aspect | Detail |
|--------|--------|
| **Render mode** | Server-rendered Razor Pages |
| **Pages** | `.cshtml` + `.cshtml.cs` in `Pages/Classic/` |
| **Data access** | Via API: jQuery AJAX calls to `/api/*` endpoints |
| **Auth** | JWT cookie read by middleware; `[Authorize]` on page models |
| **Grid** | DataTables 2.2.2 (jQuery plugin) with AJAX data source |
| **URLs** | `/classic/movies`, `/classic/users` |
| **Login** | Bootstrap modal → AJAX POST → JWT cookie |

---

## Routing

### Blazor UI Routes

| Route | Page | Auth |
|-------|------|------|
| `/` | `Home.razor` (login form / welcome) | Mixed |
| `/movies` | `MoviePages/Index.razor` | Mixed |
| `/movies/create` | `MoviePages/Create.razor` | Authenticated |
| `/movies/edit?id={id}` | `MoviePages/Edit.razor` | Authenticated |
| `/movies/details?id={id}` | `MoviePages/Details.razor` | Public |
| `/movies/delete?id={id}` | `MoviePages/Delete.razor` | Authenticated |
| `/usermanagement` | `UserManagement.razor` | Admin |

Blazor routes are defined via `@page` directives in each `.razor` file. The `Routes.razor` component in `Components/` handles routing with `InteractiveServer` render mode.

### Classic UI Routes

| Route | Page | Auth |
|-------|------|------|
| `/classic/movies` | `Pages/Classic/Movies/Index.cshtml` | Public |
| `/classic/users` | `Pages/Classic/Users/Index.cshtml` | Admin |

Classic routes are standard Razor Pages with `@page` directive. They are registered in `Program.cs` via `builder.Services.AddRazorPages()` and `app.MapRazorPages()`.

### API Routes

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `GET` | `/api/movies` | Public | List movies |
| `GET` | `/api/movies/{id}` | Public | Movie detail |
| `POST` | `/api/movies` | Required | Create movie |
| `PUT` | `/api/movies/{id}` | Required | Update movie |
| `DELETE` | `/api/movies/{id}` | Required | Delete movie |
| `POST` | `/api/auth/login` | Public | Login (returns JWT cookie) |
| `POST` | `/api/auth/logout` | Public | Logout (clears JWT cookie) |
| `POST` | `/api/auth/register` | Public | Register new user |
| `GET` | `/api/admin/users` | Admin | List users |
| `POST` | `/api/admin/users` | Admin | Create user |
| `PUT` | `/api/admin/users/{id}` | Admin | Update user |
| `PUT` | `/api/admin/users/{id}/password` | Admin | Change user password |
| `DELETE` | `/api/admin/users/{id}` | Admin | Delete user |

---

## Authentication Flow

### Shared JWT Auth

Both UIs use the same JWT Bearer authentication scheme:

1. User submits credentials via login form
2. Server validates credentials and issues a JWT token
3. Token is stored in an `HttpOnly` cookie named `auth_token`
4. JWT middleware reads the cookie on every request (`OnMessageReceived` in `Program.cs`)
5. The `ClaimsPrincipal` is populated with user identity and role claims

### Blazor UI Login

```
Home.razor (login form) → JS interop → authService.login(email, password)
→ POST /api/auth/login → JWT cookie → page reload
```

The login form is rendered on the Home page when the user is unauthenticated. It calls `window.authService.login` via `IJSRuntime.InvokeAsync`, which sends a POST request to `/api/auth/login`.

### Classic UI Login

```
Classic Layout (login modal) → jQuery AJAX → POST /api/auth/login → JWT cookie → page reload
```

The login modal is in `Pages/Classic/_Layout.cshtml`. It validates the form client-side, sends JSON via AJAX, and reloads the page on success.

### Auth State Propagation

**Blazor**: `AuthenticationState` cascading parameter is used in all pages. The `AuthorizeView` component and `[Authorize]` attributes control visibility.

**Classic UI**: Auth state is passed to JavaScript via `data-auth` attribute on `<body>`:
```html
<body data-auth="true" data-user="admin@example.com">
```
JavaScript reads it via `document.body.getAttribute("data-auth")` and stores it in `window.__classicAuth`.

### UI Preference Cookie

Both UIs set a `ui_preference` cookie when the user switches:

| Value | Meaning |
|-------|---------|
| `blazor` | Use Blazor UI (default) |
| `classic` | Use Classic UI |

The cookie is set by the "Switch to Blazor UI" / "Switch to Classic UI" links and has a 30-day expiry. The Classic layout reads it to show the correct brand link.

---

## Classic UI — File Structure

### Layout (`Pages/Classic/`)

| File | Purpose |
|------|---------|
| `_Layout.cshtml` | Master layout: navbar, login modal, script loading, auth state |
| `_ViewStart.cshtml` | Sets `_Layout.cshtml` as the layout page |

### Movies Page (`Pages/Classic/Movies/`)

| File | Purpose |
|------|---------|
| `Index.cshtml` | HTML table, DataTables initialization, CRUD modals |
| `Index.cshtml.cs` | `[AllowAnonymous]` page model (empty — data comes from API) |

### Users Page (`Pages/Classic/Users/`)

| File | Purpose |
|------|---------|
| `Index.cshtml` | HTML table, DataTables initialization, all CRUD modals |
| `Index.cshtml.cs` | `[Authorize(Roles = "Admin")]` page model, loads users via `UserManager` and serializes to JSON |

### JavaScript (`wwwroot/js/`)

| File | Purpose |
|------|---------|
| `classic-movies.js` | DataTables initialization, AJAX CRUD operations, toast notifications |
| `classic-users.js` | DataTables initialization, inline data binding, AJAX CRUD operations |

### Client Libraries (`wwwroot/lib/`)

| Library | Version | Usage |
|---------|---------|-------|
| jQuery | 3.7.1 | DOM manipulation, AJAX, DataTables dependency |
| DataTables | 2.2.2 | Table rendering, sorting, pagination |
| DataTables Bootstrap 5 | 2.2.2 | Bootstrap 5 theme integration |
| Bootstrap | 5.3.3 | Layout, modals, toasts, navigation |

---

## Classic UI — JavaScript Architecture

### Script Loading Order

In `_Layout.cshtml` (bottom of `<body>`):

1. jQuery
2. DataTables
3. DataTables Bootstrap 5 integration
4. Bootstrap bundle
5. **Inline script** — sets `window.__classicAuth` and `window.__classicUserName` from `data-*` attributes
6. `classic-movies.js` — guarded: runs only if `#moviesTable` exists
7. `classic-users.js` — guarded: runs only if `#usersTable` exists
8. **Inline script** — `switchToBlazor()`, `logout()`, `showLoginModal()`, login form handler
9. `@RenderSectionAsync("Scripts")` — page-specific scripts

### Data Flow

**Movies**: DataTables loads data via AJAX from `/api/movies` (GET). CRUD operations are event-delegated on `#moviesTable` and send AJAX requests to the same API.

**Users**: The page model serializes users to JSON in `OnGet()` and passes it via `@Html.Raw`:
```html
<script>
    window.__currentUserId = "...";
    window.__usersData = [...];
</script>
```
DataTables initializes with `data: window.__usersData` (inline data). CRUD operations send AJAX to `/api/admin/users`.

### Event Delegation Pattern

All DataTables row buttons use event delegation:
```js
$("#usersTable").on("click", ".edit-user-btn", function () { ... });
```
This avoids issues with DataTables re-rendering rows on pagination/sort.

---

## Blazor UI — Component Architecture

### Page Components

| Component | Route | Key Features |
|-----------|-------|-------------|
| `Home.razor` | `/` | Login form (unauthenticated) or welcome + UI switcher (authenticated) |
| `MoviePages/Index.razor` | `/movies` | QuickGrid, filter by title, Create/Edit/Details/Delete buttons |
| `MoviePages/Create.razor` | `/movies/create` | EditForm with validation |
| `MoviePages/Edit.razor` | `/movies/edit` | EditForm, Save + Back to List |
| `MoviePages/Details.razor` | `/movies/details` | Detail view, Edit + Back to List |
| `MoviePages/Delete.razor` | `/movies/delete` | Confirm + Delete form |
| `UserManagement.razor` | `/usermanagement` | User list, Create/Edit/Password/Delete |

### Shared Components

| Component | Purpose |
|-----------|---------|
| `Layout/MainLayout.razor` | App shell with NavMenu |
| `Layout/NavMenu.razor` | Navigation with role-based links |
| `App.razor` | Application root |
| `Routes.razor` | Route configuration |
| `RedirectToLogin.razor` | Auth redirect |
| `NotAuthorized.razor` | Access denied page |

---

## Auth Guards Comparison

### Blazor UI

| Guard | Location |
|-------|----------|
| `[Authorize]` attribute | `Create.razor`, `Edit.razor`, `Delete.razor` |
| `[Authorize(Roles = "Admin")]` attribute | `UserManagement.razor` |
| `authState?.User.Identity?.IsAuthenticated` | `Index.razor` (show/hide buttons) |
| `User.IsInRole("Admin")` | `NavMenu.razor` (show Users link) |
| `AuthorizeView` component | `App.razor` (conditional rendering) |

### Classic UI

| Guard | Location |
|-------|----------|
| `[Authorize(Roles = "Admin")]` attribute | `Users/Index.cshtml.cs` |
| `[AllowAnonymous]` attribute | `Movies/Index.cshtml.cs` |
| `User.Identity?.IsAuthenticated` | `_Layout.cshtml` (navbar login/logout, Users link) |
| `window.__classicAuth.isAuthenticated` | `classic-movies.js` (show/hide Edit/Delete buttons) |
| `window.__currentUserId` | `classic-users.js` (prevent self-delete) |

---

## User Management — CRUD Operations

### Blazor UI (`UserManagement.razor`)

| Operation | Implementation |
|-----------|---------------|
| **Create** | `UserManager.CreateAsync` + `AddToRoleAsync` |
| **Edit** | `UserManager.UpdateAsync` + role sync |
| **Change Password** | `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync` |
| **Delete** | `UserManager.DeleteAsync` |
| **Self-protection** | Delete button hidden when `vm.Id == currentUserId && vm.IsAdmin` |

### Classic UI (`/classic/users`)

| Operation | Implementation |
|-----------|---------------|
| **Create** | AJAX POST `/api/admin/users` |
| **Edit** | AJAX PUT `/api/admin/users/{id}` |
| **Change Password** | AJAX PUT `/api/admin/users/{id}/password` |
| **Delete** | AJAX DELETE `/api/admin/users/{id}` |
| **Self-protection** | Delete button not rendered when `row.id === window.__currentUserId` |

### API (`AdminController.cs`)

The `AdminController` is a `[Authorize(Roles = "Admin")]` API controller with primary constructor. It uses `UserManager<User>` directly and provides all CRUD endpoints.

### Blazor UI

The Blazor movie pages (`Index.razor`, `Create.razor`, `Edit.razor`, `Details.razor`, `Delete.razor`) now use `IHttpClientFactory` to call the REST API instead of accessing the database directly. A named `HttpClient` named `"BlazorApi"` is registered in `Program.cs` with an `AuthCookieHandler` that forwards the `auth_token` cookie from the current HTTP context, so authenticated calls (create/edit/delete) pass through JWT auth. Public GET requests for movie list and details are `[AllowAnonymous]`.

**Flow:**

```
Blazor Page → IHttpClientFactory.CreateClient("BlazorApi") → AuthCookieHandler
→ HttpClient → /api/movies/* → MoviesController → Database
```

The `AuthCookieHandler` is a `DelegatingHandler` in `Components/Handlers/AuthCookieHandler.cs`.

All 245 tests pass, including the 18 `BlazorMoviesPageTests` that now exercise the controller layer via `MoviesController` directly.

---

## UI Switcher

### How it works

1. Both UIs render a "Switch to X UI" link
2. Clicking the link sets `ui_preference` cookie
3. On the next page load, the preference is read from the cookie
4. The app redirects to the appropriate UI root

### Blazor UI → Classic UI

In `NavMenu.razor`:
```html
<a class="nav-link" href="/classic/movies" 
   onclick="document.cookie='ui_preference=classic; path=/; max-age=' + (30*24*60*60)">
    Classic UI
</a>
```

### Classic UI → Blazor UI

In `_Layout.cshtml`:
```js
function switchToBlazor(e) {
    e.preventDefault();
    document.cookie = "ui_preference=blazor; path=/; max-age=" + (30 * 24 * 60 * 60);
    window.location.href = "/";
}
```

---

## Test Coverage

| Category | Tests | Focus |
|----------|-------|-------|
| `Controllers/AdminControllerTests.cs` | 17 | User CRUD, password change, error cases |
| `ClassicUiPages/ClassicUsersPageTests.cs` | 12 | Page model OnGet, JSON serialization, auth |
| `ClassicUiPages/ClassicMoviesPageTests.cs` | 2 | OnGet, AllowAnonymous attribute |
| `ClassicUiPages/ClassicAuthIntegrationTests.cs` | 9 | Login/logout, JWT tokens, role claims |
| `BlazorUiTests/BlazorMoviesPageTests.cs` | 17 | Movie CRUD, auth guards |
| `BlazorUiTests/BlazorUserManagementTests.cs` | 19 | User CRUD, password, self-protection |
| `BlazorUiTests/BlazorAuthFlowTests.cs` | 18 | Auth state, role guards, NavMenu |

All tests use in-memory database and real `UserManager<User>` via `IServiceProvider` setup.