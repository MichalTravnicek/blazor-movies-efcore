using System.ComponentModel.DataAnnotations;
using BlazorWebAppMovies.Models;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class MovieValidationTests
{
    [Fact]
    public void Movie_WithValidData_PassesValidation()
    {
        var movie = new Movie
        {
            Id = 1,
            Title = "Inception",
            ReleaseDate = new DateOnly(2010, 7, 16),
            Genre = "Sci-fi",
            Price = 12.99m,
            MovieRatingId = 3
        };

        var results = ValidateModel(movie);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB")] // Too short (min length 3)
    public void Movie_InvalidTitle_FailsValidation(string? title)
    {
        var movie = new Movie
        {
            Title = title,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Action",
            Price = 10m,
            MovieRatingId = 2
        };

        var results = ValidateModel(movie);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Movie.Title)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")] // Doesn't match pattern
    [InlineData("sci-fi")] // Doesn't start with uppercase
    public void Movie_InvalidGenre_FailsValidation(string? genre)
    {
        var movie = new Movie
        {
            Title = "Valid Title",
            ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = genre,
            Price = 10m,
            MovieRatingId = 2
        };

        var results = ValidateModel(movie);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Movie.Genre)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public void Movie_OutOfRangePrice_FailsValidation(decimal price)
    {
        var movie = new Movie
        {
            Title = "Valid Title",
            ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Action",
            Price = price,
            MovieRatingId = 2
        };

        var results = ValidateModel(movie);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Movie.Price)));
    }



    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
