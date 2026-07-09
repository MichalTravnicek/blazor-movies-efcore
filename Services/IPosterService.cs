namespace BlazorWebAppMovies.Services;

/// <summary>
/// Service for fetching and managing movie posters.
/// Supports automatic download from TMDB API and manual upload.
/// </summary>
public interface IPosterService
{
    /// <summary>
    /// Searches TMDB for a poster by movie title and year.
    /// Returns the full TMDB image URL, or null if nothing found.
    /// </summary>
    Task<string?> FetchPosterUrlAsync(string title, int? year);

    /// <summary>
    /// Saves a manually uploaded poster file locally and returns its URL.
    /// </summary>
    Task<string?> SavePosterAsync(int movieId, IFormFile file);

    /// <summary>
    /// Gets the local poster path for a given movie ID and size.
    /// Returns null if no local poster exists.
    /// </summary>
    string? GetLocalPosterPath(int movieId, string size = "thumb");
}
