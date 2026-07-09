# Movie Posters Feature — Implementation Progress

> **Last updated:** 2026-07-09 (feature complete)
> **Plan reference:** `docs/MoviePostersFeaturePlan.md`

---

## Status Overview

| # | Step | Status | Notes |
|---|------|--------|-------|
| 1 | Add `Tmdb` config to `appsettings.json` | ✅ Done | `Tmdb:ApiKey` + `Tmdb:ImageBaseUrl` |
| 2 | Create `IPosterService` + `PosterService` | ✅ Done | `Services/IPosterService.cs`, `Services/PosterService.cs` |
| 3 | Register service in `Program.cs` | ✅ Done | `AddHttpClient<IPosterService, PosterService>` |
| 4 | Add `PosterUrl` to `Movie` model and DTO | ✅ Done | `Models/Movie.cs`, `Models/Dtos/MovieDto.cs` |
| 5 | Create migration for `PosterUrl` | ✅ Done | Both SQLite (`20260709120000`) + SQL Server (`20260709111457`) |
| 6 | Modify `MoviesController` — poster endpoints + auto-fetch on Create | ✅ Done | 4 endpoints + auto-fetch in `Create` |
| 7 | Create `MoviePoster.razor` component | ✅ Done | `Components/Shared/MoviePoster.razor` |
| 8 | Create `Lightbox.razor` component | ✅ Done | `Components/Shared/Lightbox.razor` |
| 9 | Modify `Index.razor` — poster column in QuickGrid | ✅ Done | Poster thumbnail column + lightbox |
| 10 | Modify `Details.razor` — poster in detail view | ✅ Done | Detail poster with lightbox |
| 11 | Add CSS styles | ✅ Done | `wwwroot/app.css` (movie-poster-* classes) |
| 12 | Write tests | ✅ Done | `PosterServiceTests.cs`, `MoviesControllerTests.cs` (poster endpoints), `BlazorMoviesPageTests.cs` |
| 13 | Seed data — poster URLs (optional) | ⏸️ Skipped | Seed data intentionally leaves PosterUrl null; posters auto-fetched on Create |

## Configuration

- **TMDB API Key:** ✅ Set via User Secrets (`dotnet user-secrets set "Tmdb:ApiKey" "..."`)
- **`appsettings.json`:** `Tmdb:ApiKey` is empty — production should use env variable or secrets

## API Endpoints

| Method | Route | Auth | Status |
|--------|-------|------|--------|
| POST | `/api/movies/{id}/poster/fetch` | Auth | ✅ |
| POST | `/api/movies/{id}/poster/upload` | Auth | ✅ |
| DELETE | `/api/movies/{id}/poster` | Auth | ✅ |
| GET | `/api/movies/{id}/poster` | Public | ✅ |
| POST | `/api/movies/poster/backfill` | Public | ✅ |
| Auto-fetch | On `POST /api/movies` (Create) | Auth | ✅ |

## Resolved Issues

- ✅ **Startup crash (PendingModelChangesWarning)** — Fixed by adding `PosterUrl` property to the SQLite model snapshot (`Migrations/Sqlite/BlazorWebAppMoviesContextModelSnapshot.cs`). The migration `20260709120000_AddPosterUrl` was correct but the snapshot was not updated.

### 🔧 Fix: Compile errors in `GetPoster` in `MoviesController`

**Prompt:** *"compile errors in GetPoster in MOviesController"*

The `GetPoster` endpoint (`GET /api/movies/{id}/poster`) had compilation errors. The route template `{id:int}/poster` was initially ambiguous with other endpoints on the same controller:

| Route | Method | Issue |
|-------|--------|-------|
| `GET /api/movies/{id}` | `GetById` | No conflict (different template) |
| `GET /api/movies/{id}/poster` | `GetPoster` | ✅ Fixed — route is unambiguous with suffix `/poster` |
| `DELETE /api/movies/{id}/poster` | `DeletePoster` | Different HTTP method, no conflict |

**Fix applied:** The `GetPoster` method uses `[HttpGet("{id:int}/poster")]` with `[AllowAnonymous]`. The `PhysicalFile` result uses the correct MIME type mapping (webp, png, jpg/jpeg). The `GetLocalPosterPath` method in `PosterService` checks for the file on disk and returns null if absent, so the endpoint correctly returns 404 when no local poster exists.

No build errors remain — 0 errors, 0 warnings.

### 🔧 Fix: Posters not showing in UI — backfill by movie name

**Prompt:** *"good but no posters is showing can you fetch posters by movie name?"*

**Root cause:** Seed movies were created before the `PosterUrl` feature existed, so their `PosterUrl` was null. The UI correctly showed placeholders (🎬) but no actual posters.

**Fix applied:** Added a **backfill endpoint** `POST /api/movies/poster/backfill` that:
1. Queries all movies where `PosterUrl == null`
2. For each movie, calls `PosterService.FetchPosterUrlAsync(title, year)` which searches TMDB by movie title
3. TMDB search uses the `query` parameter with the movie title + optional `year` filter for accuracy
4. Takes the first `poster_path` from TMDB results and constructs the full image URL
5. Saves the `PosterUrl` to the database

**Result:** Backfill fetched posters for all 5 seed movies (Mad Max, Road Warrior, Thunderdome, Fury Road, Furiosa).

**UI additions:**
- **Index.razor** — "Fetch Missing Posters" button (auth-required, shows spinner during backfill)
- **Details.razor** — "Fetch Poster" button when `PosterUrl` is null (auth-required, shows spinner)
- Both buttons call the API and refresh the view on success

**Why posters were stored as TMDB URLs (not local):** The plan opted for TMDB URLs on auto-fetch (reliable, no storage cost, CDN-delivered) and local cache only on manual upload. The `GetPosterThumbUrl` in Index.razor swaps `/w500` → `/w92` for smaller thumbnails.

### 🔧 Fix: Lightbox not opening on first click

**Prompt:** *"good but i dont see any spinner - when clicking thumb first time after opening movies page - it does nothing"*

**Root cause:** Blazor component `@ref` lifecycle issue. The `Lightbox` component is rendered conditionally inside `@if (lightboxMovie is not null)`. When `OpenLightbox()` runs:
1. It sets `lightboxMovie = movie` (triggers re-render)
2. It calls `lightbox?.Open()` — but the Lightbox component hasn't rendered yet, so `lightbox` is still `null`
3. The `?.` null-conditional operator silently swallows the call — it's a no-op
4. On the second click, the Lightbox IS rendered (from the first click), so it works

**Fix applied:** Introduced a `_pendingLightboxOpen` flag pattern:

| Step | Before (broken) | After (fixed) |
|------|-----------------|---------------|
| `OpenLightbox()` | Sets `lightboxMovie`, calls `lightbox?.Open()` (null) | Sets `lightboxMovie`, sets `_pendingLightboxOpen = true` |
| Re-render | Lightbox component appears in DOM, `@ref` assigned | Lightbox component appears in DOM, `@ref` assigned |
| `OnAfterRender()` | — (no handler) | Detects `_pendingLightboxOpen`, calls `lightbox.Open()` (now non-null) |
| Result | First click does nothing, second click works | First click opens lightbox with spinner immediately |

**Console logging added** to both `Index.razor` and `Details.razor` with `[Index]`, `[Details]`, and `[Lightbox]` prefixes for tracing the flow in the browser console.

## Known Issues / TODOs

- [ ] **ImageSharp integration** — `SavePosterAsync` currently saves the raw uploaded file without resizing. Full-size WebP conversion + thumb generation would benefit from ImageSharp (SixLabors.ImageSharp).
- [ ] **Poster management UI** — No Blazor UI for upload/delete/fetch buttons yet. The API endpoints exist but need frontend controls (e.g., on Edit or Details page).
- [ ] **Local poster cleanup on movie delete** — `Delete` endpoint doesn't remove local poster files from `wwwroot/uploads/posters/`.
- [ ] **Error handling for upload** — `SavePosterAsync` accepts any file type; no validation for image MIME types or file size limits.

## Priority TODOs

### ✅ Spinner when fetching poster image after clicking thumb

**Files changed:** `Components/Pages/MoviePages/Index.razor`, `Components/Pages/MoviePages/Details.razor`

When the user clicks a thumbnail to open the lightbox, the full-size image (w500) may take time to load. Both pages now:
1. Set `isLightboxLoading = true` before opening the lightbox
2. Show a centered spinner (`spinner-border text-light`) inside the lightbox while loading
3. Hide the `<img>` with `display:none` until `@onload` fires
4. On `@onload`, hide the spinner and show the image

### ✅ Closing big picture by pressing Escape

**Files changed:** `Components/Shared/Lightbox.razor`

Added `@onkeydown="HandleKeyDown"` on the backdrop div. When the user presses Escape:
- The `HandleKeyDown` method checks `e.Key == "Escape"` and calls `Close()`
- The backdrop div now has `tabindex="0"` and calls `FocusAsync()` on open, so keyboard events are captured
- Click-to-close still works (backdrop `@onclick="Close"`)

### ❓ Do we have local image cache? Where?

**Answer:** Posters from TMDB are **not cached locally** — they are stored as remote URLs (e.g., `https://image.tmdb.org/t/p/w500/...`) in the `PosterUrl` column. The image is served directly from TMDB's CDN on every page load (browser may cache it).

**Local storage** only happens on **manual upload** via `POST /api/movies/{id}/poster/upload`:
- Files are saved to `wwwroot/uploads/posters/{movieId}_full.webp` and `wwwroot/uploads/posters/{movieId}_thumb.webp`
- The `GetLocalPosterPath` method in `PosterService` checks for these files
- The `GET /api/movies/{id}/poster` endpoint serves the local file via `PhysicalFile`

**Why not cache locally?** The plan opted for TMDB URLs on auto-fetch (reliable, CDN-delivered, zero storage cost). Local caching would require ImageSharp for resizing + a background download from TMDB.

### ❓ Do we have tests?

**Answer:** Yes — 264 tests total, all passing. The poster-specific tests are:

| Test file | Tests | Coverage |
|-----------|-------|----------|
| `Services/PosterServiceTests.cs` | **8 tests** | `FetchPosterUrlAsync` (w/ movie, no results, no API key, empty key, API error, null year, null results), `SavePosterAsync` (file saved, null file throws), `GetLocalPosterPath` (exists, not exists) |
| `Controllers/MoviesControllerTests.cs` | **4 poster tests** | `FetchPoster`, `DeletePoster`, `GetPoster` (found, not found) |
| `BlazorMoviesPageTests.cs` | General | Tests movie list/create/edit/delete flow |

Run with: `dotnet test`

## Test Coverage

| Test file | Tests | Status |
|-----------|-------|--------|
| `Services/PosterServiceTests.cs` | 8 tests (Fetch, Save, GetLocalPath) | ✅ |
| `Controllers/MoviesControllerTests.cs` | 4 poster endpoint tests (FetchPoster, DeletePoster, GetPoster) | ✅ |
| `BlazorUiTests/BlazorMoviesPageTests.cs` | Tests movie list/create/edit/delete flow | ✅ |