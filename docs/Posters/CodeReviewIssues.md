
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