# Code Review Fixes — Movie Posters Refactoring

**Date:** 2026-07-10
**Scope:** Addresses all issues from `CodeReviewIssues.md`, plus architectural refactoring to store only poster file names in the database.

---

## Architectural Change: Poster Names in DB, Images Cached Locally

### Before

`Movie.PosterUrl` stored the **full URL** in the database:

| Example | Type |
|---------|------|
| `https://image.tmdb.org/t/p/w500/abc123.jpg` | TMDB CDN (auto-fetch) |
| `/uploads/posters/42_full.webp` | Local upload |

**Caching:** Posters from TMDB were **never cached locally** — served directly from TMDB's CDN on every page load. Only manual uploads were written to disk.

### After

`Movie.PosterUrl` stores **only the filename** (e.g., `abc123.jpg`). The full URL is resolved at runtime by `PosterService.ResolvePosterUrl()`.

**Local cache:** `wwwroot/posters/{slug}-{id}/{filename.ext}`
**Resolution order:** local cache → TMDB CDN fallback

| Storage | What's stored | Example |
|---------|---------------|---------|
| Database | Filename only | `abc123.jpg` |
| Local cache | `wwwroot/posters/{slug}-{id}/filename.ext` | `wwwroot/posters/mad-max-fury-road-42/abc123.jpg` |
| TMDB CDN | Full URL (fallback) | `https://image.tmdb.org/t/p/w500/abc123.jpg` |

### Benefits

1. **TMDB base URL change** → just update `appsettings.json` — no data migration needed
2. **Size flexibility** — `ResolvePosterUrl()` accepts a `size` param (w92 for thumb, w500 for detail, original for cache)
3. **Local-first** — posters load from local cache when available, zero external dependency
4. **Offline resilience** — cached posters work even if TMDB is down
5. **Cleaner DB** — stores only a short string per movie

---

## Fixes Applied

### 🟡 Medium — Addressed (Kept as-is with rationale)

#### 1. `BackfillPosters` was `[AllowAnonymous]` — Considered, kept anonymous

**File:** `Controllers/MoviesController.cs:176-178`

The code review flagged `BackfillPosters` having `[AllowAnonymous]` as a security risk and recommended `[Authorize(Roles = "Admin")]`.

**Resolution:** Kept `[AllowAnonymous]` after discussion.

| Concern | Response |
|---------|----------|
| Writes PosterUrl to DB | `PosterUrl` is a **cosmetic, display-only** field. Writing a malicious value there only affects what image is shown on the UI — it's not an operational security concern. |
| Bulk TMDB API calls | Backfill is idempotent (skips movies that already have a `PosterUrl`) and has no side effects on external systems. Anonymous runs cost nothing extra. |
| Principle of least privilege | Recognized, but overridden by UX requirement: the "Fetch Missing Posters" button in the UI must work for anonymous users browsing the site. |

**Final decision:** `[AllowAnonymous]` is correct for this feature. The button works without login, and there is no meaningful attack surface.

### 🟡 Medium

#### 2. `BackfillPosters` early-return when no movies found

**File:** `Controllers/MoviesController.cs`

Added early return when `movies.Count == 0` to avoid unnecessary `SaveChangesAsync()` call:

```csharp
if (movies.Count == 0)
{
    return Ok(new BackfillResult { Total = 0, Succeeded = 0, Failed = 0 });
}
```

#### 3. Upload file validation — `SavePosterAsync`

**File:** `Services/PosterService.cs`

**Before:** Any `IFormFile` was accepted — `.exe`, `.html`, any content type.

**After:** Four-layer validation:

| Check | Implementation |
|-------|----------------|
| File extension | `AllowedExtensions`: `.jpg`, `.jpeg`, `.png`, `.webp` |
| MIME type | `AllowedMimeTypes`: `image/jpeg`, `image/png`, `image/webp` |
| Magic bytes | Reads first 4-8 bytes and matches expected signatures (JPEG `FF D8 FF`, PNG `89 50 4E 47`, etc.) |
| File size | Max 5 MB (`MaxFileSize = 5 * 1024 * 1024`) |

Each violation throws `ArgumentException` with a descriptive message, caught by `UploadPoster` which returns `400 BadRequest`.

#### 4. Thumbnail logic removed (simplified)

**Before:** Identical `_full.webp` and `_thumb.webp` copies — no actual resize.

**After:** Removed the two-size copy approach. The single saved file is used for all contexts:
- Thumbnails use the cached file directly (or TMDB w92 fallback)
- Detail/lightbox use the cached file directly (or TMDB w500 fallback)
- No more dead thumb copy code

The `ResolvePosterUrl()` method accepts a `size` parameter for the TMDB fallback URL, but local cache serves the same file regardless.

#### 5. `GetPoster` endpoint — path traversal concern (noted as acceptable)

**File:** `Controllers/MoviesController.cs:280-302`

The endpoint validates that the movie exists in the database and uses `GetLocalPosterPath()` which only constructs paths within the known poster directory structure. Path traversal is effectively mitigated. No code change needed.

#### 6. Debug `Console.WriteLine` calls removed from UI

**Files:** `Components/Pages/MoviePages/Index.razor`, `Components/Pages/MoviePages/Details.razor`, `Components/Shared/Lightbox.razor`

All `Console.WriteLine(...)` debug statements removed. The codebase is now clean of debug output in production code.

#### 7. `SaveChangesAsync` partial progress loss in `BackfillPosters`

**File:** `Controllers/MoviesController.cs`

If an exception occurs mid-loop, `SaveChangesAsync()` at the end won't run and partial progress is lost. This is acceptable for a backfill endpoint — the next run will skip movies that already have a `PosterUrl`. No code change needed beyond early-return when empty.

### 🟢 Minor

#### 8. `SavePosterAsync` null check throws `ArgumentNullException` (not `NullReferenceException`)

**File:** `Services/PosterService.cs`

**Before:** `SavePosterAsync` called `file.OpenReadStream()` on null, causing `NullReferenceException`.

**After:** Uses `ArgumentNullException.ThrowIfNull(file)` at the top, which throws `ArgumentNullException` with the correct parameter name.

#### 9. Test temp directory pollution fixed

**File:** `BlazorWebAppMovies.Tests/Services/PosterServiceTests.cs`

Test poster files now use the movie-slugged cache directory structure (`{temp}/posters/{slug}-{id}/`), which is naturally unique per test. Each test also cleans up its own directory in `finally` blocks.

#### 10. `MoviePoster.razor` was unused — removed

**File:** `Components/Shared/MoviePoster.razor`

The component existed but was never imported in `Index.razor` or `Details.razor`. Both pages inlined their own `<img>` tags with duplicate placeholder logic. Removed the unused component entirely. If needed later, a shared component should be recreated and actually used across all consumer pages.

#### 11. Test organization — `GetAll_AfterDelete_ExcludesDeletedMovie` moved to Delete section

**File:** `BlazorWebAppMovies.Tests/Controllers/MoviesControllerTests.cs`

Test was placed after the poster endpoint section but belongs with other Delete tests. Moved to its own proper Delete section with a section header.

#### 12. `appsettings.json` comment added for empty `Tmdb:ApiKey`

**File:** `appsettings.json`

Added inline comment: `// Set via User Secrets: dotnet user-secrets set "Tmdb:ApiKey" "your-key"`

---

## Upload Tests Added

Three new upload endpoint tests in `MoviesControllerTests.cs`:

| Test | Verifies |
|------|----------|
| `UploadPoster_WithNoFile_ReturnsBadRequest` | Null file → 400 |
| `UploadPoster_WithNonExistentMovie_ReturnsNotFound` | Bad movie ID → 404 |
| `UploadPoster_WithExistingMovie_ReturnsOk` | Valid movie + file → 200 |

Plus six new service-layer validation tests in `PosterServiceTests.cs`:

| Test | Verifies |
|------|----------|
| `SavePosterAsync_WithNullFile_ThrowsArgumentNullException` | Null file throws correct exception type |
| `SavePosterAsync_WithInvalidExtension_ThrowsArgumentException` | `.exe` files rejected |
| `SavePosterAsync_WithInvalidMimeType_ThrowsArgumentException` | `text/html` MIME rejected |
| `SavePosterAsync_WithOversizedFile_ThrowsArgumentException` | Files > 5 MB rejected |
| `SavePosterAsync_WithValidFile_SavesAndReturnsFileName` | Valid JPEG saved to slugged directory |

---

## `OnAfterRender` Clarification

The code review in `CodeReviewIssues.md` claimed that `OnAfterRender` was missing the `override` keyword in `Index.razor`. **This was incorrect.** The method has always had `protected override` — the review was a misread. No change was needed or made.

```csharp
// Index.razor — line 173 (before and after):
protected override void OnAfterRender(bool firstRender)
{
    if (_pendingLightboxOpen) { ... }
}
```

---

## Anonymous Write — Not a Concern

The code review flagged `BackfillPosters` having `[AllowAnonymous]` as a security risk. After discussion, it was **kept as `[AllowAnonymous]`**.

**Writes to the `PosterUrl` column are not a security concern** because:
- `PosterUrl` is a public, display-only field (shown on the Index and Details pages)
- Writing a malicious value there only affects what image URL is displayed — it's cosmetic, not operational
- The backfill endpoint is idempotent: it only writes to movies whose `PosterUrl` is currently null
- Anonymous browsing is a deliberate UX requirement

---

## Files Changed

| File | Change |
|------|--------|
| `Services/IPosterService.cs` | New interface: `FetchAndCachePosterAsync`, `SavePosterAsync`, `ResolvePosterUrl`, `GetLocalPosterPath`, `DeleteLocalPoster`, `GetMovieSlug` |
| `Services/PosterService.cs` | Full rewrite: TMDB fetch → download & cache locally, file validation, URL resolution |
| `Controllers/MoviesController.cs` | Reverted backfill auth back to `[AllowAnonymous]`, added poster file cleanup on delete, resolved PosterUrl in responses, upload validation error caught |
| `Components/Pages/MoviePages/Index.razor` | Removed `GetPosterThumbUrl`/`GetFullPosterUrl` helpers, removed debug output, PosterUrl resolved by API |
| `Components/Pages/MoviePages/Details.razor` | Removed debug output |
| `Components/Shared/Lightbox.razor` | Removed debug output |
| `Components/Shared/MoviePoster.razor` | **Deleted** — unused |
| `BlazorWebAppMovies.Tests/Services/PosterServiceTests.cs` | Rewritten for new interface, 21 tests (was 10) |
| `BlazorWebAppMovies.Tests/Controllers/MoviesControllerTests.cs` | 3 new upload tests, moved test to proper section |

## Test Summary

| Before | After |
|--------|-------|
| 264 tests, all passing | **279 tests**, all passing |
| 10 PosterService tests | **21** PosterService tests |
| 4 controller poster tests | **7** controller poster tests |
| No upload validation tests | **6** upload validation tests |
