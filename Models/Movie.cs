using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorWebAppMovies.Models;

public class Movie
{
    public int Id { get; set; }

    [Required]
    [StringLength(60, MinimumLength = 3)]
    [RegularExpression(@".*\S+.*", ErrorMessage = "Title cannot be only whitespace.")]
    public string? Title { get; set; }

    public DateOnly ReleaseDate { get; set; }

    [Required]
    [StringLength(30)]
    [RegularExpression(@"^[A-Z]+[a-zA-Z()\s-]*$")]
    public string? Genre { get; set; }

    [Range(0, 100)]
    [DataType(DataType.Currency)]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Price { get; set; }

    /// <summary>
    /// Poster image URL (TMDB or local path).
    /// </summary>
    public string? PosterUrl { get; set; }

    /// <summary>
    /// Foreign key to the <see cref="Models.MovieRating"/> lookup table.
    /// </summary>
    [Required]
    public int MovieRatingId { get; set; }

    /// <summary>
    /// Navigation property to the associated movie rating.
    /// </summary>
    [ForeignKey(nameof(MovieRatingId))]
    public MovieRating? MovieRating { get; set; }
}
