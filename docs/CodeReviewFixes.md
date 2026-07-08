# Code Review & Fixes - BlazorWebAppMovies

## Code Review Summary (2026-07-06)

### Issues Found During Code Quality Review

#### 🔴 Critical
1. ~~**SeedData used IServiceProvider instead of IDbContextFactory**~~ ✅ Fixed
2. ~~**README migration instructions didn't match actual structure**~~ ✅ Fixed (migrations now in `Migrations/Sqlite/` and `Migrations/SqlServer/`)
3. **JWT token expiry set to 24 years** — Security issue in `AuthController.cs` (`AddYears(24)`). ✅ Fixed to `AddHours(24)`

#### 🟠 High
4. ~~**Unused DbContext subclasses**~~ ✅ Now used by DesignTimeDbContextFactory
5. **Auto-migration runs in all environments** — `context.Database.Migrate()` ran on every startup. ✅ Fixed: wrapped in `if (app.Environment.IsDevelopment())`
6. **Contradictory migration endpoint usage** — `UseMigrationsEndPoint()` was in non-Development block. ✅ Fixed: moved to Development block

#### 🟡 Medium
7. **Mixed namespace styles** — Some files used file-scoped, others block-scoped. ✅ Fixed: all now use file-scoped
8. ~~**Movie.Rating regex not case-insensitive**~~ ✅ Fixed with `(?i)`
9. ~~**Movie.Title could be whitespace-only**~~ ✅ Fixed with regex
10. ~~**README default URLs didn't match launchSettings.json**~~ ✅ Fixed
11. **Redundant DbContext registrations** — `Program.cs` registered both base and derived factories plus `AddScoped`. ✅ Fixed: only base factory registered
12. **BlazorWebAppMoviesContext used untyped `DbContextOptions`** — Could cause DI issues. ✅ Fixed to `DbContextOptions<BlazorWebAppMoviesContext>`

### Fixes Applied

| # | Issue | File(s) Changed |
|---|-------|-----------------|
| 1 | JWT expiry 24y → 24h | `Controllers/AuthController.cs` |
| 2 | Auto-migration only in Development | `Program.cs` |
| 3 | MigrationsEndPoint in Development block | `Program.cs` |
| 4 | Remove redundant DbContext registrations | `Program.cs` |
| 5 | Typed DbContextOptions | `Data/BlazorWebAppMoviesContext.cs`, `Data/BlazorWebAppMoviesContextSqlite.cs`, `Data/BlazorWebAppMoviesContextSqlServer.cs`, `Data/DesignTimeDbContextFactory.cs` |
| 6 | Mixed namespace styles → file-scoped | `Data/BlazorWebAppMoviesContext.cs`, `Data/BlazorWebAppMoviesContextSqlite.cs`, `Data/BlazorWebAppMoviesContextSqlServer.cs`, `Data/DesignTimeDbContextFactory.cs` |
| 7 | DbContextProviderTests updated for typed options | `BlazorWebAppMovies.Tests/DatabaseTests/DbContextProviderTests.cs` |

## Patterns & Conventions

- **Namespace style**: File-scoped preferred (`namespace BlazorWebAppMovies.Data;`)
- **DbContext access**: Use `IDbContextFactory<BlazorWebAppMoviesContext>` pattern (via `AddDbContextFactory`)
- **SeedData**: Accepts `IDbContextFactory` + `IServiceProvider` (for Identity seeding)
- **Ratings**: Case-insensitive validation via `(?i)` flag in regex
- **Titles**: Must contain at least one non-whitespace character
- **JWT expiry**: 24 hours (not 24 years)
- **Migrations**: Only run in Development environment
- **DbContextOptions**: Always use typed `DbContextOptions<TContext>`

## Project Structure

```
BlazorWebAppMovies/
├── Components/           # Blazor UI components
│   ├── Layout/           # MainLayout, NavMenu
│   ├── Pages/            # Movie CRUD pages, Home, UserManagement, Weather
│   ├── NotAuthorized.razor
│   └── RedirectToLogin.razor
├── Controllers/          # API controllers (AuthController)
├── Data/                 # DbContext, SeedData, DesignTimeDbContextFactory
├── Migrations/           # EF Core migrations (Sqlite/, SqlServer/)
├── Models/               # Movie, User (Identity)
├── Properties/           # launchSettings.json
├── docker/               # Docker Compose for SQL Server
├── BlazorWebAppMovies.Tests/
│   ├── DatabaseTests/    # DbContext, SeedData, MovieValidation, AuthFlow tests
│   └── AuthorizationTests/ # Role-based access tests
└── wwwroot/              # Static assets

```
---

**Commit:** `bf35562` — Blazor UI switch to API for CRUD  
**Date:** 2026-07-08  
**Scope:** 20 staged files reviewed; 5 🔴 Must Fix items addressed

---

## Summary

Code review of the "Blazor UI switch to API for CRUD" commit identified 5 critical issues and 5 "should consider" items. This report documents the initial issues found and the fixes applied.

---

## 1. Missing Leading Slash on Not-Found Navigation

**Issue:** `NavigateTo("notfound")` uses a relative path, which resolves relative to the current URL. For example, navigating from `/movies/details/5` would go to `/movies/notfound` instead of `/notfound`.

**Fix:** Changed all occurrences to `NavigateTo("/notfound")` with absolute path.

**Files affected:**
- `Components/Pages/MoviePages/Details.razor` — 2 occurrences (lines 56, 61)
- `Components/Pages/MoviePages/Edit.razor` — 3 occurrences (lines 93, 108, 137)
- `Components/Pages/MoviePages/Delete.razor` — 2 occurrences (lines 73, 78)

---

## 2. Edit Form Rating Input Inconsistency

**Issue:** `Edit.razor` used `<InputText>` for the Rating field, allowing free-text entry with no validation against the allowed set (G/PG/PG-13/R/NC-17). The `Create.razor` page correctly used a `<select>` dropdown with predefined options.

**Fix:** Replaced the `<InputText>` in `Edit.razor` with the same `<select>` pattern used in `Create.razor` (G/PG/PG-13/R/NC-17 options with a "Select rating..." placeholder).

**Files affected:**
- `Components/Pages/MoviePages/Edit.razor` — lines 46-48 replaced

---

## 3. Duplicated ErrorResponse Class

**Issue:** The `ErrorResponse` private class was defined identically in both `Create.razor` (lines 104-107) and `Edit.razor` (lines 154-157), violating DRY.

**Fix:** Extracted to a shared DTO at `Models/Dtos/ErrorResponse.cs`. The `_Imports.razor` already includes `@using BlazorWebAppMovies.Models.Dtos`, so both pages picked up the shared type automatically.

**Files affected:**
- `Models/Dtos/ErrorResponse.cs` — new file created
- `Components/Pages/MoviePages/Create.razor` — private class removed
- `Components/Pages/MoviePages/Edit.razor` — private class removed

---

## 4. Role Removal Without Rollback (User Management)

**Issue:** In `UserManagement.razor`, when changing a user's role, the code called `RemoveFromRolesAsync` followed by `AddToRoleAsync`. If `AddToRoleAsync` failed, the user was left with no roles at all.

**Fix:** Added error checking on `RemoveFromRolesAsync` result. If `AddToRoleAsync` fails, the original roles are restored via `AddToRolesAsync` before surfacing the error.

**Files affected:**
- `Components/Pages/UserManagement.razor` — lines 366-384

---

## 5. Console.log Leaking User Data (Classic UI)

**Issue:** The `Index.cshtml` for the classic users page contained a `console.log` statement that dumped the full `window.__usersData` array (including internal user IDs) to the browser's developer console, exposing internal identifiers to any user with F12.

**Fix:** Removed the `console.log` line entirely. The page data (via `window.__usersData`) is still available for the DataTable — it was only the debug logging that was removed.

**Files affected:**
- `Pages/Classic/Users/Index.cshtml` — line 168 removed

---

## 6. Items Considered But Not Addressed

The following 🟡 "Should Consider" items from the initial review were not implemented as they are lower priority:

| # | Issue | Reasoning |
|---|-------|-----------|
| 6 | AuthCookieHandler doesn't encode cookie value | Edge case; special chars in tokens are uncommon |
| 7 | Index.razor LoadMovies() has no error handling | Silent failure is acceptable for a read-only list page |
| 8 | Broad catch blocks mapping all errors to "not found" | Pattern consistent across all movie pages; refactor would be larger scope |
| 9 | jQuery/DataTables dependency in classic UI | Pre-existing; not part of the commit's scope |
| 10 | `location.reload()` after CRUD instead of DataTable update | Pre-existing pattern; not part of the commit's scope |

---

## Validation

All changes compile without errors or warnings. The project diagnostics show 0 errors and 0 warnings.

---

# Code Review Fixes

**Commit:** `bf35562` — Blazor UI switch to API for CRUD  
**Date:** 2026-07-08  
**Scope:** 20 staged files reviewed; 10 items addressed (5 🔴 Must Fix + 5 🟡 Should Consider)

---

## 🔴 Must Fix Items

### 1. Missing Leading Slash on Not-Found Navigation

**Issue:** `NavigateTo("notfound")` uses a relative path, resolving relative to the current URL. From `/movies/details/5` it would go to `/movies/notfound` instead of `/notfound`.

**Fix:** Changed all occurrences to `NavigateTo("/notfound")`.

**Files affected:**
- `Components/Pages/MoviePages/Details.razor` — 2 occurrences
- `Components/Pages/MoviePages/Edit.razor` — 3 occurrences
- `Components/Pages/MoviePages/Delete.razor` — 2 occurrences

---

### 2. Edit Form Rating Input Inconsistency

**Issue:** `Edit.razor` used `<InputText>` for Rating, allowing free-text entry. `Create.razor` used a `<select>` with validated options (G/PG/PG-13/R/NC-17).

**Fix:** Replaced `<InputText>` in `Edit.razor` with the same `<select>` pattern from `Create.razor`.

**Files affected:**
- `Components/Pages/MoviePages/Edit.razor`

---

### 3. Duplicated ErrorResponse Class

**Issue:** Private `ErrorResponse` class defined identically in both `Create.razor` and `Edit.razor`.

**Fix:** Extracted to shared `Models/Dtos/ErrorResponse.cs`. The `_Imports.razor` already imports `BlazorWebAppMovies.Models.Dtos`.

**Files affected:**
- `Models/Dtos/ErrorResponse.cs` — **new file**
- `Components/Pages/MoviePages/Create.razor` — private class removed
- `Components/Pages/MoviePages/Edit.razor` — private class removed

---

### 4. Role Removal Without Rollback

**Issue:** In `UserManagement.razor`, `RemoveFromRolesAsync` then `AddToRoleAsync` — if the add failed, the user was left with no roles.

**Fix:** Check `RemoveFromRolesAsync` result. If `AddToRoleAsync` fails, restore original roles via `AddToRolesAsync` before surfacing error.

**Files affected:**
- `Components/Pages/UserManagement.razor` — lines 366-384

---

### 5. Console.log Leaking User Data (Classic UI)

**Issue:** `Index.cshtml` dumped full `window.__usersData` (including internal IDs) to browser console.

**Fix:** Removed the `console.log` line.

**Files affected:**
- `Pages/Classic/Users/Index.cshtml` — line 168 removed

---

## 🟡 Should Consider Items (Now Addressed)

### 6. AuthCookieHandler Doesn't Encode Cookie Value

**Issue:** Cookie value was appended directly to the Cookie header, which could break if the token contained special characters (e.g. `=`, `;`, spaces).

**Fix:** Wrapped value with `Uri.EscapeDataString()`.

**Files affected:**
- `Components/Handlers/AuthCookieHandler.cs`

---

### 7. Index.razor LoadMovies() Has No Error Handling

**Issue:** If the API call failed, the exception bubbled up unhandled, causing a blazor error page.

**Fix:** Wrapped the API call in a try-catch. On failure, the movies list stays empty and the UI shows an empty table.

**Files affected:**
- `Components/Pages/MoviePages/Index.razor`

---

### 8. Broad Catch Blocks Mapping All Errors to "Not Found"

**Issue:** `Details.razor`, `Edit.razor`, and `Delete.razor` all caught **every** exception and redirected to `/notfound`. A network error was indistinguishable from a 404.

**Fix:** Split the catch into two:
- `catch (HttpRequestException ex) when (ex.StatusCode == NotFound)` → redirect to `/notfound`
- `catch` (generic) → stay on page, show "Movie not found" UI gracefully instead of hard redirect

**Files affected:**
- `Components/Pages/MoviePages/Details.razor`
- `Components/Pages/MoviePages/Edit.razor` (LoadMovie method)
- `Components/Pages/MoviePages/Delete.razor`

---

### 9. jQuery/DataTables Dependency in Classic UI

**Issue:** The classic UI pages introduced a jQuery/DataTables dependency for client-side sorting/pagination.

**Resolution:** This is an accepted design choice for the classic UI. The DataTables library provides a richer table experience than plain HTML tables. No code change needed — noted for awareness.

---

### 10. `location.reload()` After CRUD Instead of DataTable Refresh

**Issue:** After every CRUD operation in `classic-users.js`, the page was fully reloaded via `location.reload()`, discarding the DataTable's state.

**Fix:** Added a `reloadUsersTable()` function that fetches fresh data from `/api/admin/users` and updates the DataTable in-place via `clear().rows.add(data).draw()`. Both `saveForm()` and the delete handler use this instead of `location.reload()`.

**Files affected:**
- `wwwroot/js/classic-users.js`

---

## Validation

All changes compile without errors or warnings. Project diagnostics: **0 errors, 0 warnings**.