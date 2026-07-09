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