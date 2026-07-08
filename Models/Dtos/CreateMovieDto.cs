using System.ComponentModel.DataAnnotations;

namespace BlazorWebAppMovies.Models.Dtos;

/// <summary>
/// Input DTO for creating a movie — no Id, includes validation attributes.
/// </summary>
public class CreateMovieDto
{
    [Required]
    [StringLength(60, MinimumLength = 3)]
    [RegularExpression(@".*\S+.*", ErrorMessage = "Title cannot be only whitespace.")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    [RegularExpression(@"^[A-Z]+[a-zA-Z()\s-]*$")]
    public string Genre { get; set; } = string.Empty;

    [Range(0, 100)]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    public DateOnly ReleaseDate { get; set; }

    [Required]
    [RegularExpression(@"^(?i)(G|PG|PG-13|R|NC-17)$")]
    public string Rating { get; set; } = string.Empty;
}
