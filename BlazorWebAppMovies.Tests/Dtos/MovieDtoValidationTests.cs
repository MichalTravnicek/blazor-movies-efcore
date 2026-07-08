using System.ComponentModel.DataAnnotations;
using BlazorWebAppMovies.Models.Dtos;

namespace BlazorWebAppMovies.Tests.Dtos;

public class MovieDtoTests
{
    [Fact]
    public void MovieDto_CanBeInstantiated()
    {
        var dto = new MovieDto
        {
            Id = 1,
            Title = "Test",
            Genre = "Action",
            Price = 9.99m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };

        Assert.Equal(1, dto.Id);
        Assert.Equal("Test", dto.Title);
        Assert.Equal(9.99m, dto.Price);
    }

    [Fact]
    public void MovieDto_DefaultConstructor_SetsDefaults()
    {
        var dto = new MovieDto();

        Assert.Equal(0, dto.Id);
        Assert.Equal(string.Empty, dto.Title);
        Assert.Equal(string.Empty, dto.Genre);
        Assert.Equal(0m, dto.Price);
        Assert.Equal(default, dto.ReleaseDate);
        Assert.Equal(string.Empty, dto.Rating);
    }
}

public class CreateMovieDtoTests
{
    [Fact]
    public void CreateMovieDto_ValidData_PassesValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "Inception",
            Genre = "Sci-Fi",
            Price = 12.99m,
            ReleaseDate = new DateOnly(2010, 7, 16),
            Rating = "PG-13"
        };

        var results = ValidateModel(dto);
        Assert.Empty(results);
    }

    [Fact]
    public void CreateMovieDto_EmptyTitle_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Title)));
    }

    [Fact]
    public void CreateMovieDto_TitleTooShort_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "AB", // MinimumLength = 3
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Title)));
    }

    [Fact]
    public void CreateMovieDto_WhitespaceTitle_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "   ",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Title)));
    }

    [Fact]
    public void CreateMovieDto_InvalidGenre_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "Valid Movie",
            Genre = "action!", // Must start with uppercase letter, only letters/spaces/hyphens/parens
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Genre)));
    }

    [Fact]
    public void CreateMovieDto_PriceOutOfRange_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "Valid Movie",
            Genre = "Action",
            Price = 150m, // Max is 100
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Price)));
    }

    [Fact]
    public void CreateMovieDto_NegativePrice_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "Valid Movie",
            Genre = "Action",
            Price = -1m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Price)));
    }

    [Theory]
    [InlineData("G")]
    [InlineData("PG")]
    [InlineData("PG-13")]
    [InlineData("R")]
    [InlineData("NC-17")]
    [InlineData("g")]
    [InlineData("pg")]
    [InlineData("r")]
    public void CreateMovieDto_ValidRating_PassesValidation(string rating)
    {
        var dto = new CreateMovieDto
        {
            Title = "Valid Movie",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = rating
        };

        var results = ValidateModel(dto);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("ABC")]
    [InlineData("")]
    [InlineData("PG13")]
    public void CreateMovieDto_InvalidRating_FailsValidation(string rating)
    {
        var dto = new CreateMovieDto
        {
            Title = "Valid Movie",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = rating
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Rating)));
    }

    [Fact]
    public void CreateMovieDto_EmptyGenre_FailsValidation()
    {
        var dto = new CreateMovieDto
        {
            Title = "Valid Movie",
            Genre = "",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMovieDto.Genre)));
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}

public class UpdateMovieDtoTests
{
    [Fact]
    public void UpdateMovieDto_ValidData_PassesValidation()
    {
        var dto = new UpdateMovieDto
        {
            Title = "Updated Movie",
            Genre = "Drama",
            Price = 14.99m,
            ReleaseDate = new DateOnly(2023, 5, 15),
            Rating = "R"
        };

        var results = ValidateModel(dto);
        Assert.Empty(results);
    }

    [Fact]
    public void UpdateMovieDto_EmptyTitle_FailsValidation()
    {
        var dto = new UpdateMovieDto
        {
            Title = "",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateMovieDto.Title)));
    }

    [Fact]
    public void UpdateMovieDto_InvalidRating_FailsValidation()
    {
        var dto = new UpdateMovieDto
        {
            Title = "Valid Movie",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "INVALID"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateMovieDto.Rating)));
    }

    [Fact]
    public void UpdateMovieDto_PriceOutOfRange_FailsValidation()
    {
        var dto = new UpdateMovieDto
        {
            Title = "Valid Movie",
            Genre = "Action",
            Price = 101m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "G"
        };

        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateMovieDto.Price)));
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
