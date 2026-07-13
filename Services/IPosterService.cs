namespace BlazorWebAppMovies.Services;

/// <summary>
/// Service for fetching, caching, and resolving movie poster images.
/// Posters are stored locally in <c>wwwroot/posters/{slug}-{id}/filename.ext</c>
/// and the database stores only the filename (e.g., <c>abc123.jpg</c>).
/// </summary>
public interface IPosterService
{
    /// <summary>
    /// Searches TMDB for a poster by movie title and year, downloads it,
    /// caches locally under <c>wwwroot/posters/{slug}-{id}/</c>,
    /// and returns just the filename.
    /// Returns null if nothing found.
    /// </summary>
    Task<string?> FetchAndCachePosterAsync(string title, int? year, int movieId);

    /// <summary>
    /// Validates a manually uploaded poster file, saves it to the local
    /// cache, and returns the sanitized filename.
    /// </summary>
    Task<string> SavePosterAsync(int movieId, string movieTitle, IFormFile file);

    /// <summary>
    /// Resolves a poster filename to a full URL.
    /// Checks the local cache first; falls back to the TMDB CDN.
    /// Returns null when posterName is null/empty.
    /// </summary>
    string? ResolvePosterUrl(string? posterName, string movieTitle, int movieId, string size = "w500");

    /// <summary>
    /// Gets the local file-system path to a cached poster, or null if absent.
    /// </summary>
    string? GetLocalPosterPath(int movieId, string movieTitle, string? posterName);

    /// <summary>
    /// Deletes the entire local poster cache directory for a movie.
    /// </summary>
    void DeleteLocalPoster(int movieId, string movieTitle, string? posterName);

    /// <summary>
    /// Produces a filesystem-safe slug for a movie title.
    /// </summary>
    string GetMovieSlug(string title);
}
