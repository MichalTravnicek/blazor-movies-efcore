using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BlazorWebAppMovies.Services;

/// <summary>
/// Service for fetching movie posters from the TMDB API, caching them locally,
/// and resolving poster filenames to full URLs (local cache → TMDB CDN fallback).
/// </summary>
public class PosterService : IPosterService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PosterService> _logger;

    // ── Validation constants ──

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Magic bytes for image types we accept.
    /// </summary>
    private static readonly Dictionary<string, byte[]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = [0xFF, 0xD8, 0xFF],
        [".jpeg"] = [0xFF, 0xD8, 0xFF],
        [".png"] = [0x89, 0x50, 0x4E, 0x47],
        [".webp"] = [0x52, 0x49, 0x46, 0x46], // "RIFF" — WebP starts with RIFF
    };

    // ── Constants ──

    private const string PosterDir = "posters";

    public PosterService(
        HttpClient httpClient,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<PosterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    // ── Public API ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string?> FetchAndCachePosterAsync(string title, int? year, int movieId)
    {
        var posterPath = await FetchTmdbPosterPathAsync(title, year);
        if (posterPath == null) return null;

        // posterPath looks like "/abc123.jpg" — strip leading slash
        var fileName = posterPath.TrimStart('/');

        var imageBaseUrl = _configuration["Tmdb:ImageBaseUrl"] ?? "https://image.tmdb.org/t/p/";
        var downloadUrl = $"{imageBaseUrl.TrimEnd('/')}/original{posterPath}";

        var slug = GetMovieSlug(title);
        var cacheDir = GetMovieCacheDir(slug, movieId);
        Directory.CreateDirectory(cacheDir);

        var destPath = Path.Combine(cacheDir, fileName);

        try
        {
            var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destPath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("Cached poster for '{Title}' (ID {MovieId}) → {Path}",
                title, movieId, destPath);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download poster image for '{Title}' from {Url}",
                title, downloadUrl);
            return fileName; // Still return the filename — we'll fall back to TMDB CDN
        }
    }

    /// <inheritdoc />
    public async Task<string> SavePosterAsync(int movieId, string movieTitle, IFormFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length == 0)
            throw new ArgumentException("Uploaded file is empty.", nameof(file));

        if (file.Length > MaxFileSize)
            throw new ArgumentException($"File size exceeds the maximum allowed size of {MaxFileSize / 1024 / 1024} MB.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' is not allowed. Accepted types: {string.Join(", ", AllowedExtensions)}");

        var contentType = file.ContentType ?? "application/octet-stream";
        if (!AllowedMimeTypes.Contains(contentType))
            throw new ArgumentException($"MIME type '{contentType}' is not allowed. Accepted types: {string.Join(", ", AllowedMimeTypes)}");

        // Validate magic bytes
        await using var validationStream = file.OpenReadStream();
        var header = new byte[8];
        var bytesRead = await validationStream.ReadAsync(header.AsMemory(0, header.Length));

        if (bytesRead < 4)
            throw new ArgumentException("File is too small to be a valid image.");

        if (!ValidMagicBytes(ext, header))
            throw new ArgumentException("File content does not match the declared file type.");

        // Reset stream position for the actual save
        validationStream.Position = 0;
        var slug = GetMovieSlug(movieTitle);
        var cacheDir = GetMovieCacheDir(slug, movieId);
        Directory.CreateDirectory(cacheDir);

        // Sanitize the filename: keep only alphanumeric + dash + dot
        var safeFileName = SanitizeFileName(file.FileName);
        var destPath = Path.Combine(cacheDir, safeFileName);

        await using (var destStream = new FileStream(destPath, FileMode.Create))
        {
            await validationStream.CopyToAsync(destStream);
        }

        _logger.LogInformation("Saved uploaded poster for movie {MovieId} → {Path}", movieId, destPath);
        return safeFileName;
    }

    /// <inheritdoc />
    public string? ResolvePosterUrl(string? posterName, string movieTitle, int movieId, string size = "w500")
    {
        if (string.IsNullOrEmpty(posterName))
            return null;

        var slug = GetMovieSlug(movieTitle);
        var localPath = GetLocalPathBySlug(movieId, slug, posterName);
        if (localPath != null)
            return localPath;

        // Fall back to TMDB CDN
        var imageBaseUrl = _configuration["Tmdb:ImageBaseUrl"] ?? "https://image.tmdb.org/t/p/";
        return $"{imageBaseUrl.TrimEnd('/')}/{size}/{posterName}";
    }

    /// <inheritdoc />
    public string? GetLocalPosterPath(int movieId, string movieTitle, string? posterName)
    {
        if (string.IsNullOrEmpty(posterName))
            return null;

        var slug = GetMovieSlug(movieTitle);
        return GetLocalPathBySlug(movieId, slug, posterName);
    }

    // ── Internal helpers ─────────────────────────────────────────

    private string? GetLocalPathBySlug(int movieId, string slug, string posterName)
    {
        var cacheDir = GetMovieCacheDir(slug, movieId);
        var candidate = Path.Combine(cacheDir, posterName);
        if (File.Exists(candidate))
        {
            var relative = Path.Combine(PosterDir, $"{slug}-{movieId}", posterName);
            return "/" + relative.Replace('\\', '/');
        }
        return null;
    }

    /// <inheritdoc />
    public void DeleteLocalPoster(int movieId, string movieTitle, string? posterName)
    {
        var slug = GetMovieSlug(movieTitle);
        var cacheDir = GetMovieCacheDir(slug, movieId);
        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, recursive: true);
            _logger.LogInformation("Deleted local poster cache for movie {MovieId}", movieId);
        }
    }

    /// <inheritdoc />
    public string GetMovieSlug(string title)
    {
        if (string.IsNullOrEmpty(title))
            return "untitled";

        // Lowercase, replace non-alphanumeric with hyphens, collapse multiple hyphens
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        return slug.Trim('-');
    }

    // ── TMDB API ─────────────────────────────────────────────────

    /// <summary>
    /// Calls TMDB search API and returns the poster_path (e.g., "/abc123.jpg"),
    /// or null if nothing found.
    /// </summary>
    private async Task<string?> FetchTmdbPosterPathAsync(string title, int? year)
    {
        var apiKey = _configuration["Tmdb:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("TMDB API key is not configured. Skipping poster fetch.");
            return null;
        }

        try
        {
            var query = Uri.EscapeDataString(title);
            var url = year.HasValue
                ? $"https://api.themoviedb.org/3/search/movie?query={query}&year={year.Value}&language=en-US"
                : $"https://api.themoviedb.org/3/search/movie?query={query}&language=en-US";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TmdbSearchResult>();

            var posterPath = result?.Results?.FirstOrDefault()?.PosterPath;
            if (string.IsNullOrEmpty(posterPath))
            {
                _logger.LogInformation("No poster found for '{Title}' ({Year})", title, year);
                return null;
            }

            _logger.LogInformation("Found poster_path for '{Title}': {Path}", title, posterPath);
            return posterPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch poster path for '{Title}' ({Year})", title, year);
            return null;
        }
    }

    // ── File-system helpers ──────────────────────────────────────

    private string GetMovieCacheDir(string slug, int movieId)
    {
        var dirName = $"{slug}-{movieId}";
        return Path.Combine(_environment.WebRootPath, PosterDir, dirName);
    }

    private static string SanitizeFileName(string fileName)
    {
        // Keep only the filename (strip any path)
        var name = Path.GetFileName(fileName);

        // Remove any characters that could cause filesystem issues
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9._-]", "_");

        // If sanitization emptied the name, use a fallback
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(sanitized)))
            sanitized = $"poster{Path.GetExtension(name)}";

        return sanitized;
    }

    private static bool ValidMagicBytes(string ext, byte[] header)
    {
        if (!MagicBytes.TryGetValue(ext, out var expected))
            return false;

        for (var i = 0; i < expected.Length && i < header.Length; i++)
        {
            if (header[i] != expected[i])
                return false;
        }

        return true;
    }

    // ── TMDB API response models ─────────────────────────────────

    private class TmdbSearchResult
    {
        [JsonPropertyName("results")]
        public List<TmdbMovieResult>? Results { get; set; }
    }

    private class TmdbMovieResult
    {
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
    }
}
