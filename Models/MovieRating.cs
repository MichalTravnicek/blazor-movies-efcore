using System.ComponentModel.DataAnnotations;

namespace BlazorWebAppMovies.Models;

/// <summary>
/// Lookup entity for MPAA motion picture ratings (G, PG, PG-13, R, NC-17).
/// </summary>
public class MovieRating
{
    public int Id { get; set; }

    /// <summary>
    /// Short code, e.g. "PG-13", "R".
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Full display name, e.g. "Parental Guidance Suggested".
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Movie> Movies { get; set; } = new List<Movie>();
}
