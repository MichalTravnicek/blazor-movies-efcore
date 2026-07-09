using AutoMapper;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Models.Mapping;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebAppMovies.Tests.Mapping;

public class MovieProfileTests
{
    private static IMapper CreateMapper()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<MovieProfile>(), loggerFactory);
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    // ── Movie → MovieDto ───────────────────────────────────────

    [Fact]
    public void Map_MovieToMovieDto_MapsAllProperties()
    {
        var mapper = CreateMapper();
        var movieRating = new MovieRating { Id = 3, Code = "PG-13", Name = "Parents Strongly Cautioned" };
        var movie = new Movie
        {
            Id = 42,
            Title = "Inception",
            Genre = "Sci-Fi",
            Price = 12.99m,
            ReleaseDate = new DateOnly(2010, 7, 16),
            MovieRatingId = 3,
            MovieRating = movieRating
        };

        var dto = mapper.Map<MovieDto>(movie);

        Assert.Equal(movie.Id, dto.Id);
        Assert.Equal(movie.Title, dto.Title);
        Assert.Equal(movie.Genre, dto.Genre);
        Assert.Equal(movie.Price, dto.Price);
        Assert.Equal(movie.ReleaseDate, dto.ReleaseDate);
        Assert.Equal("PG-13", dto.Rating);
    }

    [Fact]
    public void Map_MovieToMovieDto_HandlesNullTitle()
    {
        var mapper = CreateMapper();
        var movieRating = new MovieRating { Id = 4, Code = "R", Name = "Restricted" };
        var movie = new Movie
        {
            Id = 1,
            Title = null,
            Genre = "Action",
            Price = 9.99m,
            ReleaseDate = new DateOnly(2020, 1, 1),
            MovieRatingId = 4,
            MovieRating = movieRating
        };

        // AutoMapper maps null source to null for reference types
        var dto = mapper.Map<MovieDto>(movie);

        Assert.Null(dto.Title);
    }

    [Fact]
    public void Map_MovieListToMovieDtoList_MapsAllItems()
    {
        var mapper = CreateMapper();
        var gRating = new MovieRating { Id = 1, Code = "G", Name = "General Audiences" };
        var pgRating = new MovieRating { Id = 2, Code = "PG", Name = "Parental Guidance Suggested" };
        var movies = new List<Movie>
        {
            new() { Id = 1, Title = "A", Genre = "G", Price = 1m, ReleaseDate = new DateOnly(2020, 1, 1), MovieRatingId = 1, MovieRating = gRating },
            new() { Id = 2, Title = "B", Genre = "PG", Price = 2m, ReleaseDate = new DateOnly(2020, 2, 2), MovieRatingId = 2, MovieRating = pgRating },
        };

        var dtos = mapper.Map<List<MovieDto>>(movies);

        Assert.Equal(2, dtos.Count);
        Assert.Equal("A", dtos[0].Title);
        Assert.Equal(2, dtos[1].Id);
    }

    // ── CreateMovieDto → Movie ─────────────────────────────────

    [Fact]
    public void Map_CreateMovieDtoToMovie_MapsAllProperties()
    {
        var mapper = CreateMapper();
        var dto = new CreateMovieDto
        {
            Title = "The Matrix",
            Genre = "Action",
            Price = 14.99m,
            ReleaseDate = new DateOnly(1999, 3, 31),
            Rating = "R"
        };

        var movie = mapper.Map<Movie>(dto);

        Assert.Equal(dto.Title, movie.Title);
        Assert.Equal(dto.Genre, movie.Genre);
        Assert.Equal(dto.Price, movie.Price);
        Assert.Equal(dto.ReleaseDate, movie.ReleaseDate);
        // Rating is not mapped from DTO to entity (resolved by controller)
        // Id should not be mapped (default 0 for new entity)
        Assert.Equal(0, movie.Id);
    }

    [Fact]
    public void Map_CreateMovieDtoToMovie_IdIsDefault()
    {
        var mapper = CreateMapper();
        var dto = new CreateMovieDto
        {
            Title = "Test Movie",
            Genre = "Comedy",
            Price = 5.99m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };

        var movie = mapper.Map<Movie>(dto);

        // Ensure the Id is not accidentally set from DTO (CreateMovieDto has no Id)
        Assert.Equal(0, movie.Id);
    }

    // ── UpdateMovieDto → Movie ─────────────────────────────────

    [Fact]
    public void Map_UpdateMovieDtoToMovie_MapsAllProperties()
    {
        var mapper = CreateMapper();
        var dto = new UpdateMovieDto
        {
            Title = "Updated Title",
            Genre = "Drama",
            Price = 9.99m,
            ReleaseDate = new DateOnly(2023, 5, 15),
            Rating = "PG"
        };

        var movie = mapper.Map<Movie>(dto);

        Assert.Equal(dto.Title, movie.Title);
        Assert.Equal(dto.Genre, movie.Genre);
        Assert.Equal(dto.Price, movie.Price);
        Assert.Equal(dto.ReleaseDate, movie.ReleaseDate);
        // Rating is not mapped from DTO to entity (resolved by controller)
        Assert.Equal(0, movie.Id);
    }

    [Fact]
    public void Map_UpdateDtoOntoExistingMovie_PreservesId()
    {
        var mapper = CreateMapper();
        var existingMovie = new Movie
        {
            Id = 99,
            Title = "Original Title",
            Genre = "Original Genre",
            Price = 1.00m,
            ReleaseDate = new DateOnly(2000, 1, 1),
            MovieRatingId = 1
        };

        var updateDto = new UpdateMovieDto
        {
            Title = "Updated Title",
            Genre = "Updated Genre",
            Price = 19.99m,
            ReleaseDate = new DateOnly(2024, 12, 25),
            Rating = "R"
        };

        mapper.Map(updateDto, existingMovie);

        Assert.Equal(99, existingMovie.Id); // Id preserved
        Assert.Equal("Updated Title", existingMovie.Title);
        Assert.Equal("Updated Genre", existingMovie.Genre);
        Assert.Equal(19.99m, existingMovie.Price);
        Assert.Equal(new DateOnly(2024, 12, 25), existingMovie.ReleaseDate);
        // Rating is not mapped from DTO to entity (resolved by controller)
    }

    [Fact]
    public void Map_UpdateDtoOntoExistingMovie_PartialUpdate_OverwritesAllFields()
    {
        var mapper = CreateMapper();
        var existingMovie = new Movie
        {
            Id = 7,
            Title = "Keep Me",
            Genre = "Keep Me",
            Price = 5.00m,
            ReleaseDate = new DateOnly(2010, 6, 1),
            MovieRatingId = 2
        };

        var updateDto = new UpdateMovieDto
        {
            Title = "New Title",
            Genre = "New Genre",
            Price = 10.00m,
            ReleaseDate = new DateOnly(2020, 6, 1),
            Rating = "PG-13"
        };

        mapper.Map(updateDto, existingMovie);

        // All fields should be overwritten (UpdateMovieDto has all fields)
        Assert.Equal("New Title", existingMovie.Title);
        Assert.Equal("New Genre", existingMovie.Genre);
        Assert.Equal(10.00m, existingMovie.Price);
        Assert.Equal(new DateOnly(2020, 6, 1), existingMovie.ReleaseDate);
        // Rating is not mapped from DTO to entity (resolved by controller)
    }

    // ── Configuration validation ───────────────────────────────

    [Fact]
    public void MapperConfiguration_IsValid()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<MovieProfile>(), loggerFactory);

        // This will throw if any mapping is invalid
        config.AssertConfigurationIsValid();

        Assert.NotNull(config.CreateMapper());
    }

    [Fact]
    public void Map_Roundtrip_CreateThenRead_PreservesData()
    {
        var mapper = CreateMapper();
        var createDto = new CreateMovieDto
        {
            Title = "Roundtrip",
            Genre = "Test",
            Price = 7.50m,
            ReleaseDate = new DateOnly(2022, 3, 15),
            Rating = "PG"
        };

        // Simulate: create from DTO, save (gets Id), then read back as MovieDto
        var movie = mapper.Map<Movie>(createDto);
        movie.Id = 123; // Simulate DB-generated Id
        movie.MovieRating = new MovieRating { Id = 2, Code = "PG", Name = "Parental Guidance Suggested" };

        var readDto = mapper.Map<MovieDto>(movie);

        Assert.Equal(123, readDto.Id);
        Assert.Equal(createDto.Title, readDto.Title);
        Assert.Equal(createDto.Genre, readDto.Genre);
        Assert.Equal(createDto.Price, readDto.Price);
        Assert.Equal(createDto.ReleaseDate, readDto.ReleaseDate);
        Assert.Equal("PG", readDto.Rating);
    }
}
