namespace BlazorWebAppMovies.Models.Dtos;

/// <summary>
/// Output DTO — used when returning movie data from the API.
/// Includes all fields including Id.
/// </summary>
public class MovieDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public string Rating { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
}
