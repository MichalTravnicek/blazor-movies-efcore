# Implementation Plan: Movie Posters

## Overview

Adding movie posters to the application — automatic download from **TMDB API** (The Movie Database) based on the movie title + manual upload of a custom image. Display in the QuickGrid list and detail view with a lightbox.

## 1. Poster Source Selection: **TMDB API**

| Criteria | OMDb API | TMDB API |
|----------|----------|----------|
| Price | Free (1,000/day) | Free for non-commercial |
| Poster API | Patreon only ($) | Free, including multiple sizes |
| Image Quality | OK | Excellent, incl. backdrops |
| Documentation | Simple | Excellent, SDK friendly |
| Rate limit | 1,000/day | 50 req/s |

**✅ TMDB is the clear choice** — richer API, free, supports multiple image sizes (w92, w154, w185, w342, w500, w780, original).

## 2. Solution Architecture

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                              Application                                     │
│                                                                              │
│  Create/Edit Movie → Movie model: PosterUrl stored in DB                     │
│                        │                                                     │
│                        ▼                                                     │
│  PosterService (Services/PosterService.cs)                                   │
│    ┌──────────────────────────────────────────┐                              │
│    │  FetchPosterUrlAsync(title, year)        │  ──→ TMDB API                │
│    │  SavePosterAsync(movieId, file)          │  ──→ Local upload            │
│    │  GetPosterUrl(movieId, size)             │  ──→ URL resolver            │
│    └──────────────────────────────────────────┘                              │
│                        │                                                     │
│                        ▼                                                     │
│  Local cache: wwwroot/uploads/posters/{id}.webp                              │
│  + fallback: TMDB URL (https://image.tmdb.org/t/p/w500/...)                  │
│                                                                              │
│  Blazor UI:                                                                  │
│    - Index: thumbnail in QuickGrid column + lightbox                         │
│    - Details: large poster + lightbox                                        │
│    - Lightbox component (Bootstrap 5 modal)                                  │
└──────────────────────────────────────────────────────────────────────────────┘
```

## 3. Detailed Changes

### A. Model Layer

**`Models/Movie.cs`** — add property:
```csharp
public string? PosterUrl { get; set; }  // Poster URL (TMDB or local)
```

**`Models/Dtos/MovieDto.cs`** — add:
```csharp
public string? PosterUrl { get; set; }
```

**`Models/Dtos/CreateMovieDto.cs`** — unchanged (PosterUrl handled on the backend)
**`Models/Dtos/UpdateMovieDto.cs`** — unchanged

**`Models/Mapping/MovieProfile.cs`** — posterUrl maps automatically (same name)

### B. Backend — New Service

**`Services/IPosterService.cs`**:
```csharp
public interface IPosterService
{
    Task<string?> FetchPosterUrlAsync(string title, int? year);
    Task<string?> SavePosterAsync(int movieId, IFormFile file);
    string? GetLocalPosterPath(int movieId, string size = "thumb");
}
```

**`Services/PosterService.cs`**:
- Calls TMDB API: `GET https://api.themoviedb.org/3/search/movie?query={title}&year={year}`
- Takes the first `poster_path` from the result and returns the full URL
- On manual upload: ImageSharp for resize and save to `wwwroot/uploads/posters/`
- Generates 2 sizes: `thumb` (200px) and `full` (800px) in WebP format
- If TMDB finds nothing → returns null → UI shows placeholder

**`appsettings.json`** — new section:
```json
{
  "Tmdb": {
    "ApiKey": "your-api-key-here",
    "ImageBaseUrl": "https://image.tmdb.org/t/p/"
  }
}
```

### C. Backend — API Endpoints

**`Controllers/MoviesController.cs`** — new endpoints:

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `POST` | `/api/movies/{id}/poster/fetch` | Auth | Auto-download from TMDB by title |
| `POST` | `/api/movies/{id}/poster/upload` | Auth | Manual image upload |
| `DELETE` | `/api/movies/{id}/poster` | Auth | Delete local poster |
| `GET` | `/api/movies/{id}/poster` | Public | Returns poster image (file) |

**Modification to `Create` endpoint**: After creating a movie, automatically call `FetchPosterUrlAsync` and save to `PosterUrl`.

### D. Backend — Database Migration

- New migration: `AddColumn<string>("PosterUrl", nullable: true)` for both providers (SQLite + SQL Server)
- Seed data: poster remains null (posters will be added later or automatically)

### E. Frontend — Blazor UI

**QuickGrid in `Index.razor`** — new TemplateColumn:
```razor
<TemplateColumn Title="Poster">
    <img src="@GetPosterThumbUrl(movie)" class="movie-poster-thumb"
         style="height:60px;cursor:pointer;" @onclick="() => OpenLightbox(movie)" />
</TemplateColumn>
```

**`Details.razor`** — add poster to detail view:
```razor
<dt class="col-sm-2">Poster</dt>
<dd class="col-sm-10">
    @if (!string.IsNullOrEmpty(_movie.PosterUrl))
    {
        <img src="@_movie.PosterUrl" class="img-fluid movie-poster-detail"
             style="max-height:400px;cursor:pointer;" @onclick="() => lightbox.Open()" />
    }
    else
    {
        <div class="text-muted">No poster available</div>
    }
</dd>
```

**New component: `Components/Shared/Lightbox.razor`** — Bootstrap 5 modal:

As described in `tech-implementation-plan.md` — simple Blazor modal component, no JS dependency.

**New component: `Components/Shared/MoviePoster.razor`** — universal poster display with fallback:
```razor
@if (!string.IsNullOrEmpty(PosterUrl))
{
    <img src="@PosterUrl" alt="@AltText" class="@Class" />
}
else
{
    <div class="movie-poster-placeholder @Class">
        <span>🎬</span>
        <small>@AltText</small>
    </div>
}

@code {
    [Parameter] public string? PosterUrl { get; set; }
    [Parameter] public string AltText { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
}
```

### F. CSS Styles (`wwwroot/app.css`)

```css
.movie-poster-thumb {
    border-radius: 4px;
    object-fit: cover;
    transition: transform 0.2s;
}
.movie-poster-thumb:hover {
    transform: scale(1.1);
}
.movie-poster-placeholder {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background: #f0f0f0;
    border-radius: 4px;
    color: #999;
}
```

### G. Tests

| Test file | New tests |
|-----------|-----------|
| `Controllers/MoviesControllerTests.cs` | Test POST poster/fetch, POST poster/upload, GET poster |
| `Services/PosterServiceTests.cs` (new) | Mock TMDB API, test FetchPosterUrlAsync |
| `BlazorUiTests/BlazorMoviesPageTests.cs` | Test poster display in Index/Details |

## 4. Implementation Steps (Order)

| # | Step | Files | Estimate |
|---|------|-------|----------|
| 1 | Add `Tmdb` config to `appsettings.json` | `appsettings.json` | 5 min |
| 2 | Create `IPosterService` + `PosterService` | `Services/IPosterService.cs`, `Services/PosterService.cs` | 1 hr |
| 3 | Register service in `Program.cs` | `Program.cs` | 5 min |
| 4 | Add `PosterUrl` to `Movie` model and DTO | `Models/Movie.cs`, `Models/Dtos/MovieDto.cs` | 10 min |
| 5 | Create migration for `PosterUrl` | Migrations | 15 min |
| 6 | Modify `MoviesController` — new poster endpoints + auto-fetch on Create | `Controllers/MoviesController.cs` | 1 hr |
| 7 | Create `MoviePoster.razor` component | `Components/Shared/MoviePoster.razor` | 20 min |
| 8 | Create `Lightbox.razor` component | `Components/Shared/Lightbox.razor` | 30 min |
| 9 | Modify `Index.razor` — add poster column to QuickGrid | `Components/Pages/MoviePages/Index.razor` | 20 min |
| 10 | Modify `Details.razor` — add poster to detail view | `Components/Pages/MoviePages/Details.razor` | 15 min |
| 11 | Add CSS styles | `wwwroot/app.css` | 10 min |
| 12 | Write tests | Test files | 1 hr |
| 13 | Seed data — add poster URLs for seed movies (optional) | `Data/SeedData.cs` | 15 min |
| **Total** | | | **~4-5 hours** |

## 5. TMDB API Key

To run this, you need:
1. Register at https://www.themoviedb.org/
2. Go to Settings → API → Generate API Key
3. Insert the key into `appsettings.json` → `Tmdb:ApiKey`

⚠️ The API key should not be in the git repository — use **User Secrets** or environment variables.

## 6. Discussion answers

1. **Automatic poster download when creating a movie (by title + year), or only manual upload?** — I suggest both: auto-fetch on Create, manual upload as an option. - yes

2. **Store posters locally (in `wwwroot/uploads/`), or only reference the TMDB URL?** — Local cache is more reliable (independent of the external API), but takes up space. We can do both: local cache on upload, TMDB URL on auto-fetch. - yes

3. **TMDB API key — do you already have one, or should I set it up via User Secrets?** - no key yet

4. **Lightbox component — is a Bootstrap 5 modal sufficient, or do you want a more advanced gallery (arrows, keyboard shortcuts)?** 
    I want a nice gallery but using Lightbox.

5. **What is the best way to handle image sizes?** 
    I suggest using ImageSharp for resizing and saving to local storage.

Priority is fetching posters from API rather than uploading them manually.
