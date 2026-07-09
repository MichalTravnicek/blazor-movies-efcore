# Code Review & Fixes - BlazorWebAppMovies

## Code Review Summary (2026-07-04)

### Issues Found During Code Quality Review

#### 🔴 Critical
1. **SeedData used IServiceProvider instead of IDbContextFactory** — The app was registered with `AddDbContextFactory` pattern, but `SeedData.Initialize` manually constructed a `BlazorWebAppMoviesContext` from `IServiceProvider`, bypassing the factory.
2. **README migration instructions didn't match actual structure** — README referenced `Migrations\Sqlite` and `Migrations\SqlServer` subdirectories, but migrations were stored at root `Migrations/` level.
3. **JWT token expiry set to 24 years** — Security issue in `AuthController.cs` (`AddYears(24)`).

#### 🟠 High
4. **Unused DbContext subclasses** — `BlazorWebAppMoviesContextSqlite` and `BlazorWebAppMoviesContextSqlServer` exist but add no behavior.
5. **Auto-migration runs in all environments** — `context.Database.Migrate()` runs on every startup including production.
6. **Contradictory migration endpoint usage** — `UseMigrationsEndPoint()` used in non-Development block.

#### 🟡 Medium
7. **Mixed namespace styles** — Some files use file-scoped (`namespace X;`), others use block-scoped (`namespace X { }`).
8. **Movie.Rating regex not case-insensitive** — Only uppercase strings like `"R"`, `"PG"` were accepted.
9. **Movie.Title could be whitespace-only** — `"   "` (3 spaces) passed validation.
10. **README default URLs didn't match launchSettings.json** — HTTPS `7073` vs `7083`, HTTP `5261` vs `5216`.

#### 🔵 Low
11. **SeedData redundant null checks** — `context == null` and `context.Movie == null` checks after constructor call.

### Fixes Applied

| # | Issue | File(s) Changed |
|---|-------|-----------------|
| 1 | SeedData now uses `IDbContextFactory` | `Data/SeedData.cs`, `Program.cs` |
| 2 | README URLs corrected | `README.md` |
| 3 | Movie.Title whitespace validation added | `Models/Movie.cs` |
| 4 | Movie.Rating regex made case-insensitive (`(?i)`) | `Models/Movie.cs` |
| 5 | SeedData tests updated for new signature | `BlazorWebAppMovies.Tests/DatabaseTests/SeedDataTests.cs` |
| 6 | Movie validation test fixed (removed `"pg-13"` from invalid cases) | `BlazorWebAppMovies.Tests/DatabaseTests/MovieValidationTests.cs` |

## Patterns & Conventions

- **Namespace style**: File-scoped preferred (`namespace BlazorWebAppMovies.Data;`)
- **DbContext access**: Use `IDbContextFactory<BlazorWebAppMoviesContext>` pattern (via `AddDbContextFactory`)
- **SeedData**: Accepts `IDbContextFactory` parameter, not `IServiceProvider`
- **Ratings**: Case-insensitive validation via `(?i)` flag in regex
- **Titles**: Must contain at least one non-whitespace character

## Project Structure

```
BlazorWebAppMovies/
├── Components/           # Blazor UI components
│   ├── Layout/           # MainLayout, NavMenu
│   └── Pages/            # Counter, Home, Weather, Movie CRUD pages
├── Controllers/          # API controllers (AuthController)
├── Data/                 # DbContext, SeedData, DesignTimeDbContextFactory
├── Migrations/           # EF Core migrations
├── Models/               # Movie, User (Identity)
├── Properties/           # launchSettings.json
├── Tests/                # Test project
└── wwwroot/              # Static assets

```
---

## Code Review — Movie Ratings Migration to Database Table (2026-07-09)

### Overview
This change normalizes movie ratings from a free-text `Rating` string column into a `MovieRating` lookup table with a foreign key relationship. It affects 21 files across models, DbContext, migrations, controllers, mapping, seed data, and tests.

---

### 🔴 Issues That Should Be Fixed Before Committing

**1. SeedData `SeedRatings` method duplicates DbContext seed data — could cause migration conflicts**

In `Data/BlazorWebAppMoviesContext.cs` (lines 28-34), ratings are seeded via `HasData()` in the context's `OnModelCreating`. In `Data/SeedData.cs` (lines 109-121), the same ratings are seeded again via `AddRange()`.

When both run (migration seeds data via SQL, then SeedData runs at startup), `SeedRatings()` checks `context.MovieRating.Any()` and skips, but this is fragile. If the migration hasn't run yet (e.g., on a fresh DB), SeedData runs first and inserts ratings, then migration tries to `InsertData` — causing a duplicate PK conflict.

🛠️ **Recommendation:** Remove the `SeedRatings` method from `SeedData.cs` entirely. The ratings are already seeded via the migration's `InsertData()` and the context's `HasData()`. The `SeedMovies()` method already handles the case where ratings exist.

---

**2. Microsoft.Data.Sqlite dependency in test project may not be declared**

`BlazorWebAppMovies.Tests/DatabaseTests/MovieRatingMigrationTests.cs` uses `Microsoft.Data.Sqlite` (line 4: `using Microsoft.Data.Sqlite;`). Verify the test project's `.csproj` includes a package reference to `Microsoft.Data.Sqlite`. If not, this will fail to compile.

---

**3. SeedDataTest movies use `.Include(m => m.MovieRating)` but rely on in-memory database — missing seed**

In `BlazorWebAppMovies.Tests/DatabaseTests/SeedDataTests.cs`, test `SeedData_MoviesHaveValidData()` calls `SeedData.Initialize()` which calls `SeedMovies()`. In `SeedMovies()`, `SeedRatings()` is called first, so ratings exist in-memory. But the test calls `context.Movie.Include(m => m.MovieRating)` — this should work because `SeedData` seeds both ratings and movies in the same context.

**Wait** — actually, `SeedMovies` creates its own `DbContext` via the factory, not the test's `_context`. So `SeedData.Initialize` creates a context, seeds ratings + movies, and saves. Then the test creates a *new* context via factory. In an in-memory database, each context sees data committed by others. This should work. ✓

---

### 🟡 Observations / Minor Issues

**4. Hardcoded migration names in tests**

`MovieRatingMigrationTests.cs` hardcodes migration names:
- `_migrationName = "20260708204138_AddMovieRatingTable"`
- `_priorMigration = "20260707190302_AddUniqueTitleIndex"`

If a prior migration is ever removed or renamed, these tests break silently. Consider making these configurable or deriving them from the migration assembly.

---

**5. SQLite Down migration drops MovieRating table and MovieRatingId column — loses data**

The `Down()` method drops the MovieRatingId column and the MovieRating table entirely. Any FK relationships and data are lost on rollback. This is acceptable for a rollback scenario (it's a migration, not a backup strategy), but worth noting.

---

**6. `SqlQueryRaw` in tests uses double-quoted identifiers — SQLite-specific**

`MovieRatingMigrationTests.cs` uses SQLite-specific `PRAGMA table_info` syntax with double-quoted identifiers:
```sql
SELECT "name" FROM pragma_table_info('Movie')
WHERE "name" = 'Rating'
```
This only works on SQLite. Since these are migration tests for SQLite, this is acceptable.

---

### ✅ What's Done Well

| Area | Assessment |
|------|------------|
| **Model design** | `MovieRating` is clean with `Code` (short) and `Name` (display) fields, proper `[Required]` and `[StringLength]` attributes |
| **FK relationship** | `DeleteBehavior.Restrict` prevents orphaned MovieRows if a rating is deleted — correct choice |
| **Migration (SQLite)** | Well-structured: CreateTable → InsertData → AddColumn (with default G) → UPDATE old data → DropColumn → CreateIndex → AddForeignKey |
| **Migration (both providers)** | Separate migrations for SQLite and SQL Server, with SQL Server using nullable column for safer data migration |
| **Controller** | Properly resolves `Rating` string → `MovieRatingId` via lookup, returns 400 for invalid ratings |
| **Mapping profile** | Correctly maps `MovieRating.Code` → DTO `Rating` string, ignores FK in input DTO mappings |
| **API contract unchanged** | DTOs still expose `Rating` as a string — existing Blazor pages and API consumers need no changes |
| **Test coverage** | 6 new migration tests + all existing tests updated for the FK approach |
| **Migration tests** | Up/down/idempotency tests, empty rating defaults to G, column existence verification |
| **Feature documentation** | `docs/Feature change.md` is thorough and well-structured |
| **Existing tests** | All properly seed ratings lookup data in constructor or setup |
| **No Blazor page changes needed** | Blazor pages send/receive `Rating` string — no UI changes required |

---

### Files Changed Summary

**New files (6):**
- `Models/MovieRating.cs` — New lookup entity
- `Migrations/Sqlite/20260708204138_AddMovieRatingTable.cs` — SQLite migration
- `Migrations/Sqlite/20260708204138_AddMovieRatingTable.Designer.cs`
- `Migrations/SqlServer/20260708214605_AddMovieRatingTable.cs` — SQL Server migration
- `Migrations/SqlServer/20260708214605_AddMovieRatingTable.Designer.cs`
- `BlazorWebAppMovies.Tests/DatabaseTests/MovieRatingMigrationTests.cs` — 6 migration tests

**Modified files (15):**
- `Models/Movie.cs` — `Rating` string → `MovieRatingId` FK + navigation property
- `Data/BlazorWebAppMoviesContext.cs` — Added `DbSet<MovieRating>`, relationship config, seed data
- `Data/SeedData.cs` — Uses `MovieRatingId`, `SeedRatings()` helper
- `Controllers/MoviesController.cs` — Resolves rating string → FK in Create/Update, includes navigation property
- `Models/Mapping/MovieProfile.cs` — Maps `MovieRating.Code` for output, ignores FK for input
- `Migrations/Sqlite/BlazorWebAppMoviesContextModelSnapshot.cs` — Updated snapshot
- `Migrations/SqlServer/BlazorWebAppMoviesContextSqlServerModelSnapshot.cs` — Updated snapshot
- `BlazorWebAppMovies.csproj`
- `BlazorWebAppMovies.Tests/BlazorWebAppMovies.Tests.csproj`
- `BlazorWebAppMovies.Tests/BlazorUiTests/BlazorMoviesPageTests.cs` — Seeds ratings, uses `MovieRatingId`
- `BlazorWebAppMovies.Tests/Controllers/MoviesControllerTests.cs`
- `BlazorWebAppMovies.Tests/DatabaseTests/DbContextProviderTests.cs`
- `BlazorWebAppMovies.Tests/Database tests/MovieDbContextTests.cs`
- `BlazorWebAppMovies.Tests/DatabaseTests/MovieQueriesTests.cs`
- `BlazorWebAppMovies.Tests/DatabaseTests/MovieValidationTests.cs`
- `BlazorWebAppMovies.Tests/DatabaseTests/SeedDataTests.cs` — Uses `.Include(m => m.MovieRating)`
- `BlazorWebAppMovies.Tests/Mapping/MovieProfileTests.cs`

---

### Summary

The change is **well-structured and correctly implemented**. The main concern is the duplicate rating seeding between `SeedData.SeedRatings()` and the migration's `HasData()`, which could cause PK conflicts on fresh databases where the migration seeds data AND SeedData tries to seed the same data.

I'd recommend fixing just **issue #1** (remove redundant `SeedRatings()` from SeedData since the migration + `HasData()` handle it) and verifying the `Microsoft.Data.Sqlite` package reference. The rest is solid.

---
# 🏷️ Code Review: Movie Posters Feature

**Review date:** 2026-07-09
**Files reviewed:** 19 implementation files + 3 test files + 2 migration files + 1 doc file

---

## ✅ Overall Assessment

Solid implementation overall. Clean separation of concerns, good error handling, thoughtful UX (loading states, fallbacks). The TMDB API integration is well done with graceful degradation. Tests are comprehensive.

Below are findings organized by severity:

---

## 🔴 Critical Issues (Must Fix Before Merge)

### 1. `BackfillPosters` has `[AllowAnonymous]` — security risk

**File:** `Controllers/MoviesController.cs:166`
```csharp
[HttpPost("poster/backfill")]
[AllowAnonymous]  // ←  Should NOT be anonymous!
```
This endpoint modifies database records (writes PosterUrl) but allows unauthenticated access. Should be `[Authorize]` (or at minimum `[Authorize(Roles = "Admin")]`).

**Fix:**
```csharp
[HttpPost("poster/backfill")]
[Authorize(Roles = "Admin")]  // restrict to admins
```

---

## 🟡 Medium Issues (Should Fix)

### 2. `BackfillPosters` doesn't call `SaveChangesAsync` if no movies found

**File:** `Controllers/MoviesController.cs:168-209`

If `movies.Count == 0`, the method returns `Ok(new BackfillResult { Total = 0, Succeeded = 0, Failed = 0 })` but **never calls `SaveChangesAsync`**. The method should still call save (it's a no-op, but the method should be consistent), or better — early-return before the loop.

Also note: if there's an exception after some successful fetches, `SaveChangesAsync` at line 200 won't be reached, and **partial progress is lost**. Consider wrapping in try/finally or batching.

### 3. `SavePosterAsync` doesn't validate file type/content

**File:** `Services/PosterService.cs:76-101`

The method accepts any `IFormFile` without checking content type. This is a potential security issue — someone could upload a `.exe` or `.html` file. The file is saved into `wwwroot/uploads/posters/`, making it publicly accessible.

**Fix:** Validate file extension and MIME type:
```csharp
var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowedExtensions.Contains(ext))
    throw new ArgumentException("Invalid file type.");
```

### 4. Thumbnail is just a copy of full image, not resized

**File:** `Services/PosterService.cs:92-97`

```csharp
// Copy same file as thumb (ImageSharp would resize here)
await using (var sourceStream = new FileStream(fullPath, FileMode.Open))
await using (var thumbStream = new FileStream(thumbPath, FileMode.Create))
{
    await sourceStream.CopyToAsync(thumbStream);
}
```

The comment says "ImageSharp would resize here" but currently thumb and full are identical. The `MoviePoster.razor` component always uses the PosterUrl as-is, so there's no benefit from having two sizes. Either:
- Remove the thumb logic (simplify), or
- Add ImageSharp to properly resize (adds dependency), or
- Keep both files identical but rename them meaningfully

### 5. `GetPoster` endpoint accessible without auth but returns local files

**File:** `Controllers/MoviesController.cs:280-302`

`[AllowAnonymous]` is fine since poster images are public, but the path traversal concern: the method constructs file path from `localPath` returned by `GetLocalPosterPath`, which uses `movieId`. Since `movieId` is validated to exist, path traversal is unlikely. However, the static file serving should be consistent.

### 6. `Index.razor` — `OnAfterRender` has missing `override` keyword

**File:** `Components/Pages/MoviePages/Index.razor:174`
```csharp
protected override void OnAfterRender(bool firstRender)
```

Wait, this actually has `override`. Let me re-check... Line 174 shows `protected override void OnAfterRender(bool firstRender)` — that's correct. However, it's **indented inconsistently** with surrounding code (missing 4 spaces before the method).

### 7. `Index.razor` — hardcoded `Console.WriteLine` calls

**File:** `Components/Pages/MoviePages/Index.razor:168-188`

Multiple `Console.WriteLine` debug statements throughout. While harmless in production, they're noisy. Consider removing or guarding with `#if DEBUG`.

---

## 🟢 Minor / Nitpicks

### 8. `PosterService.FetchPosterUrlAsync` — year parameter could be misleading

**File:** `Services/PosterService.cs:43-45`

TMDB's `year` parameter filters by **release year only** (exact match). If a movie was released in a different year than expected, it may not find the poster. The `primary_release_year` parameter might be more flexible.

Not critical, but worth noting if users complain about missing posters.

### 9. TMDB API key stored in User Secrets, but fallback in appsettings is empty string

**File:** `appsettings.json:18-21`
```json
"Tmdb": {
    "ApiKey": "",
    "ImageBaseUrl": "https://image.tmdb.org/t/p/"
}
```

The empty string in config is fine — the service checks for null/empty. But consider adding a comment:
```json
"Tmdb": {
    "ApiKey": "",  // Set via User Secrets: dotnet user-secrets set "Tmdb:ApiKey" "your-key"
    "ImageBaseUrl": "https://image.tmdb.org/t/p/"
}
```

### 10. `MoviePoster.razor` is not used in `Index.razor` or `Details.razor`

**File:** `Components/Shared/MoviePoster.razor`

The component was created (step 7 in the plan), but **neither `Index.razor` nor `Details.razor` actually import or use it**. Both pages inline their own `<img>` tags with duplicate logic. The shared component is unused dead code.

**Either:**
- Refactor Index/Details to use `<MoviePoster>` component, or
- Remove the unused component.

### 11. `PosterServiceTests` uses `Path.GetTempPath()` which could cause cross-test pollution

**File:** `BlazorWebAppMovies.Tests/Services/PosterServiceTests.cs:49,158,221`

Multiple tests write to `Path.GetTempPath()/uploads/posters/`. While each test generates unique filenames (e.g., `42_full.webp`, `99_thumb.webp`), the cleanup in `SavePosterAsync_SavesFileAndReturnsUrl` deletes the entire directory. If tests run in parallel, this could cause conflicts.

**Fix:** Use a unique temp directory per test:
```csharp
var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
```

### 12. `SavePosterAsync_WithNullFile_ThrowsArgumentNullException` expects `NullReferenceException` but documents `ArgumentNullException`

**File:** `BlazorWebAppMovies.Tests/Services/PosterServiceTests.cs:213`

```csharp
await Assert.ThrowsAsync<NullReferenceException>(() => service.SavePosterAsync(1, null!));
```

This is testing that passing `null!` throws, but:
- The test name says `ArgumentNullException`
- It actually expects `NullReferenceException` (from calling `file.OpenReadStream()` on null)
- The method should check for null and throw `ArgumentNullException` explicitly

**Fix (in PosterService):**
```csharp
if (file == null) throw new ArgumentNullException(nameof(file));
```

### 13. `DeletePoster` only clears DB field, doesn't delete local file

**File:** `Controllers/MoviesController.cs:262-275`

```csharp
movie.PosterUrl = null;
await context.SaveChangesAsync();
```

The DB field is cleared, but the local file at `wwwroot/uploads/posters/{id}_full.webp` and `{id}_thumb.webp` is **not deleted**. Over time this will accumulate orphaned files.

**Fix:** Delete local files when clearing PosterUrl:
```csharp
// Also delete local files
var fullPath = Path.Combine(env.WebRootPath, "uploads/posters", $"{id}_full.webp");
var thumbPath = Path.Combine(env.WebRootPath, "uploads/posters", $"{id}_thumb.webp");
if (File.Exists(fullPath)) File.Delete(fullPath);
if (File.Exists(thumbPath)) File.Delete(thumbPath);
```

### 14. `GetAll_AfterDelete_ExcludesDeletedMovie` test is misplaced

**File:** `BlazorWebAppMovies.Tests/Controllers/MoviesControllerTests.cs:692-706`

This test is placed at the bottom after the poster endpoint section (line 645-690), but belongs with the other Delete tests. Minor organizational nit.

---

## 📊 Summary

| Category | Count | Issues |
|----------|-------|--------|
| 🔴 Critical | 1 | BackfillPosters allows anonymous write |
| 🟡 Medium | 6 | Auth on backfill, file validation, thumb logic, path traversal concern, debug output, partial progress loss |
| 🟢 Minor | 7 | Unused component, test pollution, exception type, orphaned files, indentation, missing ImageSharp, test organization |

## 🏆 What's Done Well

1. **Clean architecture**: PosterService is well-separated, DI-registered properly
2. **Graceful degradation**: All external API failures return `null`, never crash
3. **Loading states**: Spinners, disabled buttons, loading flags throughout UI
4. **Comprehensive tests**: 10 PosterService tests + 4 controller poster tests + existing tests updated
5. **Good documentation**: `FeaturePlanImplementation.md` is thorough
6. **No JS dependency**: Lightbox is pure Blazor/Bootstrap
7. **Auto-fetch on Create**: Seamless UX — posters appear automatically