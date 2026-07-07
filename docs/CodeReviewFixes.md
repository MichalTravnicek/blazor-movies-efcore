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