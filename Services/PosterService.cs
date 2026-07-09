using System.Text.Json.Serialization;

namespace BlazorWebAppMovies.Services;

/// <summary>
/// Service for fetching movie posters from the TMDB API and managing local poster storage.
/// </summary>
public class PosterService : IPosterService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PosterService> _logger;

    private const string PosterDir = "uploads/posters";

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

    /// <inheritdoc />
    public async Task<string?> FetchPosterUrlAsync(string title, int? year)
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

            var imageBaseUrl = _configuration["Tmdb:ImageBaseUrl"] ?? "https://image.tmdb.org/t/p/";
            var fullUrl = $"{imageBaseUrl.TrimEnd('/')}/w500{posterPath}";
            _logger.LogInformation("Found poster for '{Title}': {Url}", title, fullUrl);
            return fullUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch poster for '{Title}' ({Year})", title, year);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> SavePosterAsync(int movieId, IFormFile file)
    {
        var uploadsDir = Path.Combine(_environment.WebRootPath, PosterDir);
        Directory.CreateDirectory(uploadsDir);

        // Save as WebP using the movie ID — thumb and full sizes
        var thumbPath = Path.Combine(uploadsDir, $"{movieId}_thumb.webp");
        var fullPath = Path.Combine(uploadsDir, $"{movieId}_full.webp");

        // For now, save the uploaded file directly (resize with ImageSharp would be ideal)
        // We save the original as full and create a copy as thumb
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Copy same file as thumb (ImageSharp would resize here)
        await using (var sourceStream = new FileStream(fullPath, FileMode.Open))
        await using (var thumbStream = new FileStream(thumbPath, FileMode.Create))
        {
            await sourceStream.CopyToAsync(thumbStream);
        }

        _logger.LogInformation("Saved poster for movie {MovieId}", movieId);
        return $"/{PosterDir}/{movieId}_full.webp";
    }

    /// <inheritdoc />
    public string? GetLocalPosterPath(int movieId, string size = "thumb")
    {
        var fileName = $"{movieId}_{size}.webp";
        var fullPath = Path.Combine(_environment.WebRootPath, PosterDir, fileName);
        return File.Exists(fullPath) ? $"/{PosterDir}/{fileName}" : null;
    }

    // ── TMDB API response models ──

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
