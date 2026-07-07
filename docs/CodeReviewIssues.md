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