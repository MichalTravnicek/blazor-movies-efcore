☀️ Good question. Let me analyze the trade-offs.

## Analysis: Should PosterUrl be stored in the database?

### Current approach

`PosterUrl` is a nullable string column on the `Movie` table. It stores either:

- A TMDB URL: `https://image.tmdb.org/t/p/w500/abc123.jpg`
- A local path: [/uploads/posters/42_full.webp]

### Arguments for storing in DB

| Pro | Explanation |
| --- | --- |
| Simple | One field, works for both TMDB and local uploads, no special logic needed |
| Fast | No API call needed to display — just read from DB and render |
| Survives TMDB changes | If TMDB changes their API or image base URL, stored URLs still work |
| Works with API | `MovieDto.PosterUrl` is returned directly from the controller — no extra computation |
| Manual override | Admin can set a custom poster URL without touching code |

### Arguments against storing in DB

| Con | Explanation |
| --- | --- |
| TMDB URLs could break | TMDB image URLs are technically not guaranteed to be permanent (though in practice they're stable) |
| Redundancy for local files | Local path is deterministic: `/uploads/posters/{id}_full.webp` — we could compute it from the movie ID without storing |
| Cache invalidation | If we re-fetch from TMDB later (e.g., a new poster is released), we need to update the DB |
| Extra migration | Adds a column, requires a migration, more schema surface area |

### Alternative: Don't store, compute at runtime

```csharp
// In a service or extension method
public string? GetPosterUrl(Movie movie)
{
    // Check local file first (deterministic path)
    var localPath = $"/uploads/posters/{movie.Id}_full.webp";
    if (File.Exists(Path.Combine(_webRootPath, localPath.TrimStart('/'))))
        return localPath;

    // Fall back to TMDB if we have a stored poster_path
    if (movie.TmdbPosterPath != null)
        return $"{_config["Tmdb:ImageBaseUrl"]}w500{movie.TmdbPosterPath}";

    return null;
}
```

But this still needs **some** storage — at minimum the TMDB `poster_path` (e.g., `/abc123.jpg`) if we want to avoid calling the API every time.

### My recommendation

**Keep storing it, but store only the TMDB `poster_path` (not the full URL).**

Instead of storing the full TMDB URL, store just the path fragment:

| Field | Type | Example |
| --- | --- | --- |
| `PosterUrl` | `string?` | `/abc123.jpg` (TMDB poster_path) or `/uploads/posters/42_full.webp` (local) |

Then at runtime, construct the full URL:

```csharp
// In PosterService
public string ResolvePosterUrl(string? posterUrl)
{
    if (string.IsNullOrEmpty(posterUrl)) return null;
    if (posterUrl.StartsWith("/uploads/")) return posterUrl; // local
    return $"{_imageBaseUrl}w500{posterUrl}"; // TMDB
}
```

**Benefits:**

- If TMDB changes their image domain or adds a CDN, we just update `Tmdb:ImageBaseUrl` in config — no data migration needed
- Smaller data in DB
- We can serve different sizes at runtime (w92 for thumbnails, w500 for detail) by changing the size segment
- Still works for local uploads (deterministic path)

**But honestly, the current approach (storing the full URL) is fine for this app's scale.** The main practical downside is if TMDB changes their image base URL — but that's unlikely, and even if it happens, it's a one-time data fix.

**The real question is:** do you ever see a need to re-fetch posters from TMDB (e.g., when a movie gets a new theatrical release poster)? If yes, then storing just the `poster_path` makes re-fetching cleaner. If no, the current approach is perfectly adequate.

What's your take — do you want to keep it as-is, or refactor to store only the path fragment?

---

☀️ Good question. Let me think through the trade-offs.

## Current approach

`PosterUrl` is stored as a nullable string on the `Movie` table. It holds either:
- A TMDB URL: `https://image.tmdb.org/t/p/w500/abc123.jpg`
- A local path: `/uploads/posters/42_full.webp`

## Does it make sense? It depends on what you store.

### Case 1: Storing the full TMDB URL — ❌ Not ideal

```sql
PosterUrl = "https://image.tmdb.org/t/p/w500/abc123.jpg"
```

Problems:
- If TMDB changes their image CDN domain, all stored URLs break
- You can't easily switch image sizes (w92 vs w500) without string manipulation
- The URL is derivable from just the poster_path + config

### Case 2: Storing just the TMDB poster_path — ✅ Makes sense

```sql
PosterUrl = "/abc123.jpg"   -- just the poster_path from TMDB
```

Then at runtime:

```csharp
// For thumbnails (Index page):
$"https://image.tmdb.org/t/p/w92{posterPath}"

// For detail view:
$"https://image.tmdb.org/t/p/w500{posterPath}"
```

Benefits:
- TMDB base URL is in config — can change without data migration
- Can serve different sizes per context
- Still works for local uploads (path starts with `/uploads/`)

### Case 3: Not storing anything — ❌ Doesn't work

You'd have to call TMDB API every time you display a movie list/detail. That's slow, expensive, and breaks if TMDB is down.

### Case 4: Local uploads only — storing the path ✅

For manually uploaded posters, storing the path is necessary since the filename is deterministic (`{id}_full.webp`) but you need to know *that* a poster exists.

## Verdict

**Storing PosterUrl in the DB is the right call**, but I'd refactor to store only the TMDB `poster_path` fragment (not the full URL). This gives you:

1. **Size flexibility** — w92 for thumbnails, w500 for detail, w780 for lightbox
2. **Config-driven base URL** — if TMDB changes their CDN, just update `appsettings.json`
3. **No API calls at render time** — fast page loads
4. **Local uploads still work** — paths starting with `/uploads/` are served directly

Want me to update the implementation to store just the poster_path and resolve URLs at runtime?
