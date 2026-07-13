# Feature Change: Blazor UI Switched from Direct Database Access to REST API

## Overview

| Field | Value |
|-------|-------|
| **Title** | Blazor UI Pages Use API Instead of Direct DbContext Access |
| **Date** | 2026-07-08 |
| **Author** | Development Team |
| **Status** | ✅ Implemented & Tested |
| **Affected Components** | Blazor Pages (Index, Create, Edit, Details, Delete), Program.cs, AuthCookieHandler, _Imports.razor, Tests |

## Rationale

Previously, all Blazor movie pages (Index, Create, Edit, Details, Delete) injected `IDbContextFactory<BlazorWebAppMoviesContext>` directly and performed database operations inline. This bypassed the API layer entirely, meaning:

- **No consistent API contract**: The controller layer existed but the Blazor UI never exercised it for movies
- **Auth duplication**: Direct DbContext access skipped JWT auth that the API endpoints enforce
- **Tight coupling**: UI pages were coupled to EF Core and the database schema, making it harder to evolve the data layer
- **No separation of concerns**: Business logic spread across controllers and UI pages

## Changes

### Blazor Movie Pages — API Migration

All five movie pages (`Index.razor`, `Create.razor`, `Edit.razor`, `Details.razor`, `Delete.razor`) were migrated from direct `IDbContextFactory` usage to `IHttpClientFactory`:

| Page | Before | After |
|------|--------|-------|
| **Index.razor** | `Implements IAsyncDisposable`, owned `DbContext`, `IQueryable<Movie>` | `List<MovieDto>` fetched via `GET /api/movies`, client-side filtering with `StringComparison.OrdinalIgnoreCase` |
| **Create.razor** | DbContext `Add` + `SaveChangesAsync` | `POST /api/movies` with `CreateMovieDto`, handles 409 Conflict with error message |
| **Edit.razor** | DbContext `Attach` + `EntityState.Modified` + concurrency exception handling | `PUT /api/movies/{id}` with `UpdateMovieDto`, handles 409/404 responses |
| **Details.razor** | `FirstOrDefaultAsync` on DbContext | `GET /api/movies/{id}` returning `MovieDto` |
| **Delete.razor** | DbContext `Remove` + `SaveChangesAsync` | `DELETE /api/movies/{id}`, gracefully handled |

### UI Enhancements (added alongside the API migration)

- **Loading states**: All pages show `Loading...` while fetching data, with an explicit `isLoading` flag
- **Not-found handling**: Pages show "Movie not found" text or redirect to `/notfound` when the API returns null
- **Submission states**: Create/Edit/Delete buttons show a spinner during submission and are disabled via `isSubmitting` flag
- **Error messages**: Create and Edit pages display user-facing error messages from API responses (e.g. duplicate title conflicts)
- **Rating dropdown**: Create page uses a `<select>` dropdown with valid MPAA ratings instead of a free-text input
- **Default release date**: Create page pre-fills `ReleaseDate` with today's date
- **Form mode**: Removed `Enhance` (enhanced form) from `EditForm` — no longer needed since requests go through HttpClient

### New Component: `AuthCookieHandler` (`Components/Handlers/AuthCookieHandler.cs`)

A `DelegatingHandler` that forwards the `auth_token` cookie from the current HTTP context to outgoing API requests, so server-side Blazor pages can call JWT-protected endpoints without re-authenticating. It also rebuilds the request URI with the real host from the current request (since the named HttpClient uses a placeholder `BaseAddress`).

### Service Registration (`Program.cs`)

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthCookieHandler>();
builder.Services.AddHttpClient("BlazorApi", client =>
{
    client.BaseAddress = new Uri("http://localhost");
})
    .AddHttpMessageHandler<AuthCookieHandler>();
```

### Import Changes (`_Imports.razor`)

Added `@using BlazorWebAppMovies.Models.Dtos` so all Blazor pages can reference DTO types (`MovieDto`, `CreateMovieDto`, `UpdateMovieDto`).

### Data Flow

```
Blazor Page → IHttpClientFactory.CreateClient("BlazorApi") → AuthCookieHandler
→ HttpClient → /api/movies/* → MoviesController → Database
```

The `AuthCookieHandler` attaches the `auth_token` cookie for authenticated endpoints (create, edit, delete). Public GET endpoints (list, details) are `[AllowAnonymous]`.

## Testing

- `BlazorMoviesPageTests`: 18 tests (up from 17) — all exercise the controller layer via `MoviesController` directly
- All 245+ tests pass across the suite

## Rollback

To revert to direct DbContext access, revert commit `bf35562`:

```bash
git revert bf35562
```

---

# Feature Change: Movie Ratings Moved to Database

## Overview

| Field | Value |
|-------|-------|
| **Title** | Normalize Movie Ratings to a Database Lookup Table |
| **Date** | 2026-07-09 |
| **Author** | Development Team |
| **Status** | ✅ Implemented & Tested |
| **Affected Components** | Models, DbContext, Migrations, SeedData, Controllers, Blazor Pages, Tests |

## Rationale

Previously, movie ratings were stored as a plain string (`Rating` column) on the `Movie` table. This approach had several drawbacks:

- **Data integrity**: Any string could be stored (e.g. "pg13", "PG 13", "R-17") — no enforcement of valid MPAA ratings
- **DRY violation**: Rating labels and validation regexes were duplicated across DTOs, pages, and the API
- **Queryability**: Filtering by rating required string matching (`WHERE Rating = 'R'`) rather than a FK join
- **Extensibility**: Adding new ratings or localized names required code changes in multiple places

The fix moves ratings into a normalized `MovieRating` lookup table with a foreign key from `Movie`.

## Changes

### New Model: `MovieRating` (`Models/MovieRating.cs`)

```csharp
public class MovieRating
{
    public int Id { get; set; }
    public string Code { get; set; }     // e.g. "PG-13", "R"
    public string Name { get; set; }     // e.g. "Parents Strongly Cautioned"
    public ICollection<Movie> Movies { get; set; }
}
```

### Modified: `Movie` (`Models/Movie.cs`)

- **Removed**: `string Rating` property
- **Added**: `int MovieRatingId` (FK) + `MovieRating? MovieRating` navigation property

### Modified: `BlazorWebAppMoviesContext` (`Data/BlazorWebAppMoviesContext.cs`)

- Added `DbSet<MovieRating> MovieRating`
- Configured `Movie → MovieRating` relationship with `DeleteBehavior.Restrict`
- Seeded 5 MPAA ratings via `HasData()`:

| Id | Code | Name |
|----|------|------|
| 1 | G | General Audiences |
| 2 | PG | Parental Guidance Suggested |
| 3 | PG-13 | Parents Strongly Cautioned |
| 4 | R | Restricted |
| 5 | NC-17 | Adults Only |

### Modified: `MovieProfile` (`Models/Mapping/MovieProfile.cs`)

- `Movie → MovieDto`: maps `MovieRating.Code` → `Rating` string
- `CreateMovieDto → Movie`: ignores `MovieRatingId` (resolved by controller)
- `UpdateMovieDto → Movie`: ignores `MovieRatingId` (resolved by controller)

### Modified: `MoviesController` (`Controllers/MoviesController.cs`)

- `Create()`: resolves `dto.Rating` → `MovieRatingId` via `MovieRating.Code` lookup
- `Update()`: resolves `dto.Rating` → `MovieRatingId` via `MovieRating.Code` lookup
- `GetAll()` / `GetById()`: `.Include(m => m.MovieRating)` to populate the navigation property

### Modified: `SeedData` (`Data/SeedData.cs`)

- Uses `MovieRatingId` instead of `Rating` string
- Resolves rating IDs dynamically via `MovieRating.Code` lookup

## Migration Strategy

### SQLite (dev)

1. `CreateTable("MovieRating")` with seed data
2. `AddColumn<int>("MovieRatingId", defaultValue: 1)` — defaults to "G" for existing rows
3. `UPDATE Movie SET MovieRatingId = (SELECT Id FROM MovieRating WHERE Code = Movie.Rating)` — migrates existing data
4. `DropColumn("Rating")`
5. `CreateIndex + AddForeignKey`

### SQL Server (prod)

1. `CreateTable("MovieRating")` with seed data
2. `AddColumn<int?>("MovieRatingId")` — nullable to allow data migration
3. `UPDATE Movie SET MovieRatingId = (SELECT Id FROM MovieRating WHERE Code = Movie.Rating)` — migrates existing data
4. `ALTER TABLE Movie ALTER COLUMN MovieRatingId int NOT NULL` — make non-nullable
5. `DropColumn("Rating")`
6. `CreateIndex + AddForeignKey`

## Testing

### Migration Tests (`DatabaseTests/MovieRatingMigrationTests.cs`)

6 new tests using SQLite in-memory database with the `BlazorWebAppMoviesContextSqlite` context:

| Test | Description |
|------|-------------|
| `CorrectlyMapsExistingRatings` | Seeds 5 movies with old `Rating` (G, PG, PG-13, R, NC-17), applies migration, asserts each maps to correct `MovieRatingId` |
| `DefaultsToG_WhenRatingIsEmpty` | Seeds empty `Rating`, applies migration, asserts `MovieRatingId = 1` (G) via `defaultValue: 1` |
| `SeedsAllFiveRatings` | Verifies MovieRating table has all 5 MPAA ratings with correct codes and names |
| `RatingColumnNoLongerExists` | Verifies `Rating` column is dropped after migration via `PRAGMA table_info` |
| `Down_RestoresRatingColumn` | Applies migration, rolls back, verifies `Rating` column exists again |
| `UpAndDown_IsIdempotent` | Up → Down → Up cycle, verifies final state is correct |

### Existing Tests Updated

All existing 248 tests (now 254 total) continue to pass:

- Mapping tests: use `MovieRating.Code` for DTO `Rating`
- DbContext tests: seed `MovieRating` lookup table, use `MovieRatingId`
- Controller tests: seed ratings, use `Rating` string on DTOs (API contract unchanged)
- Validation tests: DTOs still validate `Rating` string (unchanged API contract)

## Rollback

To undo this migration:

```bash
# SQLite (dev)
dotnet ef migrations remove --context BlazorWebAppMoviesContextSqlite

# SQL Server (prod)
dotnet ef migrations remove --context BlazorWebAppMoviesContextSqlServer
```

This will revert to the previous migration, restoring the `Rating` string column. Existing `MovieRatingId` data will be lost.

---

# Feature Change: Movie Posters via TMDB Integration + Local Storage

## Overview

| Field | Value |
|-------|-------|
| **Title** | Movie Posters — TMDB Fetch, Manual Upload, Local Storage, Backfill |
| **Date** | 2026-07-09 |
| **Author** | Development Team |
| **Status** | ✅ Implemented, 🟡 Code Review Issues Exist (see below) |
| **Affected Components** | PosterService, IPosterService, MoviesController, Program.cs, Blazor Pages, Tests |

## Rationale

Movies without posters look empty in the UI. The feature provides:

- **Automatic poster fetching** from TMDB API on movie creation
- **Manual poster upload** for custom or missing images
- **Backfill command** to populate all existing null-PosterUrl movies in one call
- **Local storage** of uploaded/fetched images so they don't depend on TMDB uptime
- **Graceful degradation** — no poster = placeholder image, never crashes

## Architecture

```
                 ┌──────────────────────────────────────────────┐
                 │              PosterService                   │
                 │                                              │
  ┌──────────────┼──────────────────────────────────────────────┘
  │              │           │               │
  ▼              ▼           ▼               ▼
TMDB API   Local File   File System    IConfiguration
(HTTP)     (IFormFile)  (webroot)      (Tmdb:ApiKey)


                  ┌───────────────────────────────────────────────┐
                  │              MoviesController                 │
                  │  ┌─────────────────────────────────────────┐  │
                  │  │  POST /api/movies                       │  │
                  │  │  → creates movie                        │  │
                  │  │  → auto-fetches poster from TMDB        │  │
                  │  │  → stores PosterUrl (TMDB URL) in DB    │  │
                  │  └─────────────────────────────────────────┘  │
                  │  ┌─────────────────────────────────────────┐  │
                  │  │  POST /api/movies/{id}/poster/upload    │  │
                  │  │  → saves file locally (wwwroot)         │  │
                  │  │  → stores local path in PosterUrl       │  │
                  │  └─────────────────────────────────────────┘  │
                  │  ┌─────────────────────────────────────────┐  │
                  │  │  POST /api/movies/{id}/poster/fetch     │  │
                  │  │  → fetches from TMDB, stores URL in DB  │  │
                  │  └─────────────────────────────────────────┘  │
                  │  ┌─────────────────────────────────────────┐  │
                  │  │  POST /api/movies/poster/backfill       │  │
                  │  │  → batch-fetches all null PosterUrl     │  │
                  │  └─────────────────────────────────────────┘  │
                  │  ┌─────────────────────────────────────────┐  │
                  │  │  GET /api/movies/{id}/poster            │  │
                  │  │  → returns local poster file (Physical) │  │
                  │  └─────────────────────────────────────────┘  │
                  │  ┌─────────────────────────────────────────┐  │
                  │  │  DELETE /api/movies/{id}/poster         │  │
                  │  │  → clears PosterUrl in DB only          │  │
                  │  │  ⚠️  orphaned files NOT deleted          │  │
                  │  └─────────────────────────────────────────┘  │
                  └───────────────────────────────────────────────┘
```

## How Image Fetching from TMDB Works

**Service:** `PosterService.FetchPosterUrlAsync(title, year)`

1. **API key check**: Reads `Tmdb:ApiKey` from config. If empty/null, logs warning and returns `null` — graceful no-op.
2. **Search query**: Builds `GET https://api.themoviedb.org/3/search/movie?query={title}&year={year}` with Bearer token auth.
3. **Parse response**: Deserializes `TmdbSearchResult` → takes first result's `poster_path`.
4. **Build full URL**: Concatenates `Tmdb:ImageBaseUrl` (default `https://image.tmdb.org/t/p/`) + `w500` + poster path.
5. **Error handling**: Any exception (network, parse, missing) is caught, logged, and returns `null`. Never throws.
6. **Storage**: The returned URL is **stored directly in the DB** as `Movie.PosterUrl` — it's a remote TMDB URL, not a local file.

**Trigger points:**
- **Auto-fetch on Create**: `POST /api/movies` calls `FetchPosterUrlAsync` after saving the movie (lines 94-99 of MoviesController.cs).
- **Manual fetch**: `POST /api/movies/{id}/poster/fetch` — fetches and saves the URL.
- **Backfill**: `POST /api/movies/poster/backfill` — loops over all movies with `PosterUrl == null` and fetches one by one.

## How Local Saving Works

**Service:** `PosterService.SavePosterAsync(movieId, file)`

1. **Directory**: Ensures `wwwroot/uploads/posters/` exists (creates if missing).
2. **File naming**: Two files per movie:
   - `{movieId}_full.webp` — the original uploaded image
   - `{movieId}_thumb.webp` — currently an identical copy (not resized — see code review issue #4)
3. **Save**: Writes the `IFormFile` stream directly to `_full.webp`, then copies it to `_thumb.webp`.
4. **Returns**: Relative URL path `"/uploads/posters/{movieId}_full.webp"`.
5. **No validation**: Does **not** check file extension or MIME type (see code review issue #3).

**Local file serving:** `GET /api/movies/{id}/poster` uses `GetLocalPosterPath(movieId, size)` which checks if the file exists on disk, then returns it via `PhysicalFile()` with the correct MIME type (webp/png/jpg).

**Key distinction:**
- TMDB-fetched posters store a **remote URL** in `PosterUrl` (e.g. `https://image.tmdb.org/t/p/w500/abc123.jpg`)
- Uploaded posters store a **local path** in `PosterUrl` (e.g. `/uploads/posters/42_full.webp`)
- The UI renders both transparently via `<img src="@posterUrl">`

## Current Code Review Status

**Review date:** 2026-07-09 | **Files reviewed:** 19 implementation + 3 test + 2 migration + 1 doc

### Summary Table

| Severity | Count | Issues |
|----------|-------|--------|
| 🔴 **Critical** | 1 | `BackfillPosters` has `[AllowAnonymous]` — writes DB without auth |
| 🟡 **Medium** | 6 | No file validation on upload, thumb is full-size copy, `GetPoster` path traversal concern, `SaveChangesAsync` partial progress loss, hardcoded `Console.WriteLine`, `DeletePoster` orphans files |
| 🟢 **Minor** | 7 | Unused `MoviePoster.razor` component, temp dir test pollution, wrong exception type in test, orphaned files on delete, indentation, `year` vs `primary_release_year`, misplaced test |

### Key Issues at a Glance

| # | Issue | Location | Severity |
|---|-------|----------|----------|
| 1 | `[AllowAnonymous]` on `BackfillPosters` write endpoint | `MoviesController.cs:166` | 🔴 Critical |
| 2 | No file type/MIME validation on upload | `PosterService.cs:76-101` | 🟡 Medium |
| 3 | Thumbnail = exact copy of full image, not resized | `PosterService.cs:92-97` | 🟡 Medium |
| 4 | `DeletePoster` clears DB but doesn't delete local files | `MoviesController.cs:262-275` | 🟡 Medium |
| 5 | `SavePosterAsync` doesn't null-check file explicitly | `PosterService.cs:76` | 🟡 Medium |
| 6 | `Index.razor` has hardcoded `Console.WriteLine` calls | `Index.razor:168-188` | 🟡 Medium |
| 7 | `MoviePoster.razor` component is unused dead code | `Components/Shared/` | 🟢 Minor |
| 8 | Cross-test pollution from shared `Path.GetTempPath()` | `PosterServiceTests.cs` | 🟢 Minor |
| 9 | Orphaned local files on poster delete | `MoviesController.cs:262-275` | 🟢 Minor |

### What's Done Well

1. **Clean architecture**: `PosterService` is well-separated, DI-registered with typed `HttpClient` and 10s timeout
2. **Graceful degradation**: All external API failures return `null`, never crash the UI
3. **Loading states**: Spinners, disabled buttons, loading flags throughout UI
4. **Comprehensive tests**: 10 `PosterService` tests + 4 controller poster tests
5. **No JS dependency**: Lightbox is pure Blazor/Bootstrap
6. **Auto-fetch on Create**: Seamless UX — posters appear automatically after creation

## Files Changed

| File | Change |
|------|--------|
| `Services/IPosterService.cs` | **New** — interface for poster operations |
| `Services/PosterService.cs` | **New** — TMDB fetch, local save, local path resolution |
| `Controllers/MoviesController.cs` | **Modified** — added 5 poster endpoints (fetch, upload, backfill, get, delete) + auto-fetch on Create |
| `Program.cs` | **Modified** — registered `IPosterService` with typed HttpClient (10s timeout) |
| `Components/Shared/MoviePoster.razor` | **New** — shared poster display component (⚠️ unused) |
| `Components/Pages/MoviePages/Index.razor` | **Modified** — poster display, loading states, lightbox |
| `Components/Pages/MoviePages/Details.razor` | **Modified** — poster display, loading states, lightbox |
| `Components/Pages/MoviePages/Create.razor` | **Modified** — auto-fetch indicator, poster preview |
| `BlazorWebAppMovies.Tests/Services/PosterServiceTests.cs` | **New** — 10 tests for PosterService |
| `BlazorWebAppMovies.Tests/Controllers/MoviesControllerTests.cs` | **Modified** — 4 poster endpoint tests added |

## Rollback

To remove the poster feature, revert the poster-related commits:

```bash
git revert <poster-commit-hash>
```

This will remove the poster endpoints, PosterService, and all UI changes. The `PosterUrl` column on `Movie` will remain (with data) but won't be populated by any new code.