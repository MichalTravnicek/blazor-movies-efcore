# Feature Plan Implementation: Movie Posters

> **Last updated:** 2026-07-09
> **Status:** ✅ Complete

## Implementation Progress

| # | Step | Status | Files | Notes |
|---|------|--------|-------|-------|
| 1 | Add `Tmdb` config to `appsettings.json` | ✅ Done | `appsettings.json` | Existing structure already present |
| 2 | Create `IPosterService` + `PosterService` | ✅ Done | `Services/IPosterService.cs`, `Services/PosterService.cs` | TMDB API integration + local upload with WebP storage |
| 3 | Register service in `Program.cs` | ✅ Done | `Program.cs` | Registered `IPosterService`/`PosterService` with `IHttpClientFactory` |
| 4 | Add `PosterUrl` to `Movie` model and DTO | ✅ Done | `Models/Movie.cs`, `Models/Dtos/MovieDto.cs` | AutoMapper maps automatically (same name) |
| 5 | Create migration for `PosterUrl` | ✅ Done | `Migrations/Sqlite/*`, `Migrations/SqlServer/*` | SQLite: `20260709120000_AddPosterUrl`, SQL Server: `20260709111457_AddPosterUrl` |
| 6 | Modify `MoviesController` — new poster endpoints + auto-fetch on Create | ✅ Done | `Controllers/MoviesController.cs` | POST poster/fetch, POST poster/upload, DELETE poster, GET poster + auto-fetch on Create |
| 7 | Create `MoviePoster.razor` component | ✅ Done | `Components/Shared/MoviePoster.razor` | Universal poster display with fallback placeholder |
| 8 | Create `Lightbox.razor` component | ✅ Done | `Components/Shared/Lightbox.razor` | Bootstrap 5 modal, no JS dependency |
| 9 | Modify `Index.razor` — add poster column to QuickGrid | ✅ Done | `Components/Pages/MoviePages/Index.razor` | Poster thumbnail column + lightbox on click |
| 10 | Modify `Details.razor` — add poster to detail view | ✅ Done | `Components/Pages/MoviePages/Details.razor` | Detail poster with lightbox |
| 11 | Add CSS styles | ✅ Done | `wwwroot/app.css` | Poster thumb, detail, placeholder styles |
| 12 | Write tests | ✅ Done | `BlazorWebAppMovies.Tests/Services/PosterServiceTests.cs`, `BlazorWebAppMovies.Tests/Controllers/MoviesControllerTests.cs` | 264 tests total (all passing) |
| 13 | Seed data — add poster URLs (optional) | ⏸️ Skipped | — | Posters fetched automatically from TMDB on Create |

## Configuration

| Setting | Location | Value |
|---------|----------|-------|
| `Tmdb:ApiKey` | User Secrets (`dotnet user-secrets`) | ✅ Set (read-access token) |
| `Tmdb:ImageBaseUrl` | `appsettings.json` | `https://image.tmdb.org/t/p/` |

## Build Status

- **Main project:** ✅ Builds with 0 errors, 0 warnings
- **Test project:** ✅ Builds with 0 errors, 0 warnings
- **Tests:** ✅ 264/264 passing

## API Endpoints

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `POST` | `/api/movies/{id}/poster/fetch` | Auth | Auto-download poster from TMDB by movie title |
| `POST` | `/api/movies/{id}/poster/upload` | Auth | Manual image upload |
| `DELETE` | `/api/movies/{id}/poster` | Auth | Remove local poster |
| `GET` | `/api/movies/{id}/poster` | Public | Returns poster image file (if local) |
| Auto | `POST /api/movies` | Auth | Auto-fetches poster from TMDB on Create |

## Architecture

```
Movie model → PosterUrl (string) stored in DB
     ↓
PosterService
  ├── FetchPosterUrlAsync(title, year)   → TMDB API (v3 search)
  ├── SavePosterAsync(movieId, file)     → local wwwroot/uploads/posters/{id}_{size}.webp
  └── GetLocalPosterPath(movieId, size)  → local path resolver
     ↓
Storage: wwwroot/uploads/posters/{id}_{thumb|full}.webp
  + TMDB URL fallback (https://image.tmdb.org/t/p/w500/...)
```

## Component Tree

```
Index.razor
  └── QuickGrid
       └── TemplateColumn "Poster"
            ├── <img> (movie-poster-thumb) → click opens Lightbox
            └── 🎬 placeholder (when no poster)
  └── Lightbox.razor (modal overlay)

Details.razor
  ├── <img> (movie-poster-detail) → click opens Lightbox
  └── Lightbox.razor (modal overlay)

MoviePoster.razor (shared component)
  ├── <img> when PosterUrl is set
  └── placeholder div when PosterUrl is null/empty
```

## Test Coverage

### PosterServiceTests (10 tests)
- `FetchPosterUrlAsync_WithValidMovie_ReturnsFullUrl`
- `FetchPosterUrlAsync_WithNoResults_ReturnsNull`
- `FetchPosterUrlAsync_WithNoApiKey_ReturnsNull`
- `FetchPosterUrlAsync_WithEmptyApiKey_ReturnsNull`
- `FetchPosterUrlAsync_WithApiError_ReturnsNull`
- `FetchPosterUrlAsync_WithoutYear_StillWorks`
- `FetchPosterUrlAsync_WithNullResults_ReturnsNull`
- `SavePosterAsync_SavesFileAndReturnsUrl`
- `SavePosterAsync_WithNullFile_ThrowsArgumentNullException`
- `GetLocalPosterPath_WhenFileExists_ReturnsPath`
- `GetLocalPosterPath_WhenFileDoesNotExist_ReturnsNull`

### MoviesControllerTests — Poster endpoints (4 tests)
- `FetchPoster_WithExistingMovie_WhenNoPosterFound_ReturnsNotFound`
- `FetchPoster_WithNonExistentId_ReturnsNotFound`
- `DeletePoster_WithExistingMovie_ReturnsNoContent`
- `DeletePoster_WithNonExistentId_ReturnsNotFound`
- `GetPoster_WithNonExistentMovie_ReturnsNotFound`