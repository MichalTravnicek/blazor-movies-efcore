using AutoMapper;
using BlazorWebAppMovies.Controllers;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Models.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebAppMovies.Tests.Controllers;

public class MoviesControllerTests : IDisposable
{
    private readonly string _dbName;
    private readonly BlazorWebAppMoviesContext _context;
    private readonly MoviesController _controller;
    private readonly IMapper _mapper;

    public MoviesControllerTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _context = CreateContext();

        var factory = new TestDbContextFactory(CreateContext);

        _mapper = CreateMapper();
        _controller = new MoviesController(factory, _mapper);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────────

    private BlazorWebAppMoviesContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new BlazorWebAppMoviesContext(options);
    }

    private BlazorWebAppMoviesContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new BlazorWebAppMoviesContext(options);
    }

    private static IMapper CreateMapper()
    {
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<MovieProfile>(), loggerFactory);
        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }

    private async Task<Movie> SeedMovie(string title = "Test Movie")
    {
        var movie = new Movie
        {
            Title = title,
            Genre = "Action",
            Price = 9.99m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };
        _context.Movie.Add(movie);
        await _context.SaveChangesAsync();
        return movie;
    }

    /// <summary>
    /// DbContext factory that creates a fresh context from the same in-memory database.
    /// Each context instance shares the same in-memory store because the DB name is the same.
    /// </summary>
    private sealed class TestDbContextFactory : IDbContextFactory<BlazorWebAppMoviesContext>
    {
        private readonly Func<BlazorWebAppMoviesContext> _factory;
        public TestDbContextFactory(Func<BlazorWebAppMoviesContext> factory) => _factory = factory;
        public BlazorWebAppMoviesContext CreateDbContext() => _factory();
    }

    // ── GET /api/movies ────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenNoMovies_ReturnsEmptyList()
    {
        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Empty(movies);
    }

    [Fact]
    public async Task GetAll_WithMovies_ReturnsAllMovies()
    {
        await SeedMovie("Movie A");
        await SeedMovie("Movie B");
        await SeedMovie("Movie C");

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Equal(3, movies.Count);
    }

    [Fact]
    public async Task GetAll_ReturnsMoviesOrderedByReleaseDate()
    {
        await SeedMovie("Movie C"); // release date 2024-01-01
        var earlyMovie = new Movie
        {
            Title = "Movie A",
            Genre = "Action",
            Price = 5m,
            ReleaseDate = new DateOnly(2020, 6, 15),
            Rating = "G"
        };
        _context.Movie.Add(earlyMovie);
        await _context.SaveChangesAsync();

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Equal(2, movies.Count);
        Assert.Equal("Movie A", movies[0].Title); // earlier date first
        Assert.Equal("Movie C", movies[1].Title);
    }

    [Fact]
    public async Task GetAll_ReturnsCorrectDtoShape()
    {
        var movie = await SeedMovie("Shape Test");

        var result = await _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        var dto = movies[0];

        Assert.Equal(movie.Id, dto.Id);
        Assert.Equal(movie.Title, dto.Title);
        Assert.Equal(movie.Genre, dto.Genre);
        Assert.Equal(movie.Price, dto.Price);
        Assert.Equal(movie.ReleaseDate, dto.ReleaseDate);
        Assert.Equal(movie.Rating, dto.Rating);
    }

    // ── GET /api/movies/{id} ───────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsMovie()
    {
        var movie = await SeedMovie("Get By Id");

        var result = await _controller.GetById(movie.Id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MovieDto>(okResult.Value);
        Assert.Equal(movie.Id, dto.Id);
        Assert.Equal(movie.Title, dto.Title);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        var result = await _controller.GetById(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_WithZeroId_ReturnsNotFound()
    {
        var result = await _controller.GetById(0);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ── POST /api/movies ───────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedAtAction()
    {
        var dto = new CreateMovieDto
        {
            Title = "New Movie",
            Genre = "Drama",
            Price = 14.99m,
            ReleaseDate = new DateOnly(2025, 1, 1),
            Rating = "R"
        };

        var result = await _controller.Create(dto);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(MoviesController.GetById), createdAtResult.ActionName);
        Assert.Equal(201, createdAtResult.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsMovieDtoWithId()
    {
        var dto = new CreateMovieDto
        {
            Title = "New Movie",
            Genre = "Drama",
            Price = 14.99m,
            ReleaseDate = new DateOnly(2025, 1, 1),
            Rating = "R"
        };

        var result = await _controller.Create(dto);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var movieDto = Assert.IsType<MovieDto>(createdAtResult.Value);
        Assert.True(movieDto.Id > 0);
        Assert.Equal(dto.Title, movieDto.Title);
    }

    [Fact]
    public async Task Create_WithValidData_PersistsToDatabase()
    {
        var dto = new CreateMovieDto
        {
            Title = "Persist Test",
            Genre = "Comedy",
            Price = 7.99m,
            ReleaseDate = new DateOnly(2023, 6, 15),
            Rating = "PG"
        };

        await _controller.Create(dto);

        // Use a fresh context to verify persistence (avoids cached entities)
        await using var fresh = CreateFreshContext();
        var movie = await fresh.Movie.FirstOrDefaultAsync(m => m.Title == "Persist Test");
        Assert.NotNull(movie);
        Assert.Equal("Comedy", movie.Genre);
        Assert.Equal(7.99m, movie.Price);
    }

    [Fact]
    public async Task Create_SetsRouteValuesCorrectly()
    {
        var dto = new CreateMovieDto
        {
            Title = "Route Test",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };

        var result = await _controller.Create(dto);

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.NotNull(createdAtResult.RouteValues);
        Assert.True(createdAtResult.RouteValues.ContainsKey("id"));
        var id = Assert.IsType<int>(createdAtResult.RouteValues["id"]);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Create_WithInvalidModelState_ReturnsBadRequest()
    {
        var dto = new CreateMovieDto
        {
            Title = "", // Invalid: empty
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };
        _controller.ModelState.AddModelError("Title", "Required");

        var result = await _controller.Create(dto);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    // ── PUT /api/movies/{id} ───────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_UpdatesMovie()
    {
        var movie = await SeedMovie("Before Update");
        var dto = new UpdateMovieDto
        {
            Title = "After Update",
            Genre = "Sci-Fi",
            Price = 19.99m,
            ReleaseDate = new DateOnly(2025, 12, 1),
            Rating = "PG-13"
        };

        var result = await _controller.Update(movie.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updatedDto = Assert.IsType<MovieDto>(okResult.Value);
        Assert.Equal("After Update", updatedDto.Title);
        Assert.Equal(movie.Id, updatedDto.Id); // Id preserved
    }

    [Fact]
    public async Task Update_WithExistingId_PersistsChangesToDatabase()
    {
        var movie = await SeedMovie("Persist Before");
        var dto = new UpdateMovieDto
        {
            Title = "Persist After",
            Genre = "Thriller",
            Price = 12.50m,
            ReleaseDate = new DateOnly(2024, 7, 4),
            Rating = "R"
        };

        await _controller.Update(movie.Id, dto);

        // Use a fresh context to avoid tracking cache
        await using var fresh = CreateFreshContext();
        var updated = await fresh.Movie.FindAsync(movie.Id);
        Assert.NotNull(updated);
        Assert.Equal("Persist After", updated.Title);
        Assert.Equal("Thriller", updated.Genre);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        var dto = new UpdateMovieDto
        {
            Title = "No Movie",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Update(999, dto);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_PreservesId()
    {
        var movie = await SeedMovie("Id Check");
        var originalId = movie.Id;
        var dto = new UpdateMovieDto
        {
            Title = "Id Check Updated",
            Genre = "Drama",
            Price = 15m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Update(originalId, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updatedDto = Assert.IsType<MovieDto>(okResult.Value);
        Assert.Equal(originalId, updatedDto.Id);
    }

    [Fact]
    public async Task Update_DoesNotCreateNewMovie()
    {
        var movie = await SeedMovie("Count Check");
        var initialCount = await _context.Movie.CountAsync();
        var dto = new UpdateMovieDto
        {
            Title = "Count Check Updated",
            Genre = "Comedy",
            Price = 5m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "G"
        };

        await _controller.Update(movie.Id, dto);

        var count = await _context.Movie.CountAsync();
        Assert.Equal(initialCount, count); // No new row created
    }

    [Fact]
    public async Task Update_ReturnsUpdatedDtoShape()
    {
        var movie = await SeedMovie("Shape Check");
        var dto = new UpdateMovieDto
        {
            Title = "Shape Check Updated",
            Genre = "Horror",
            Price = 8.50m,
            ReleaseDate = new DateOnly(2023, 10, 31),
            Rating = "R"
        };

        var result = await _controller.Update(movie.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updatedDto = Assert.IsType<MovieDto>(okResult.Value);
        Assert.Equal("Shape Check Updated", updatedDto.Title);
        Assert.Equal("Horror", updatedDto.Genre);
        Assert.Equal(8.50m, updatedDto.Price);
        Assert.Equal(new DateOnly(2023, 10, 31), updatedDto.ReleaseDate);
        Assert.Equal("R", updatedDto.Rating);
    }

    // ── DELETE /api/movies/{id} ─────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsNoContent()
    {
        var movie = await SeedMovie("To Delete");

        var result = await _controller.Delete(movie.Id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WithExistingId_RemovesMovieFromDatabase()
    {
        var movie = await SeedMovie("Really Delete");

        await _controller.Delete(movie.Id);

        // Use a fresh context to avoid tracking cache
        await using var fresh = CreateFreshContext();
        var deleted = await fresh.Movie.FindAsync(movie.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        var result = await _controller.Delete(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_OnlyRemovesSpecifiedMovie()
    {
        var movie1 = await SeedMovie("Keep Me");
        var movie2 = await SeedMovie("Delete Me");

        await _controller.Delete(movie2.Id);

        var remaining = await _context.Movie.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep Me", remaining[0].Title);
    }

    // ── Duplicate title tests ─────────────────────────────────

    [Fact]
    public async Task Create_WithDuplicateTitle_ReturnsConflict()
    {
        await SeedMovie("Unique Title");

        var dto = new CreateMovieDto
        {
            Title = "Unique Title",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Create(dto);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task Create_WithCaseInsensitiveDuplicateTitle_ReturnsConflict()
    {
        await SeedMovie("Inception");

        var dto = new CreateMovieDto
        {
            Title = "inception",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_WithWhitespacePaddedDuplicateTitle_ReturnsConflict()
    {
        await SeedMovie("Inception");

        var dto = new CreateMovieDto
        {
            Title = "  Inception  ",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Create(dto);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_WithDuplicateTitleOnDifferentMovie_ReturnsConflict()
    {
        await SeedMovie("Existing Movie");
        var movie2 = await SeedMovie("Other Movie");

        var dto = new UpdateMovieDto
        {
            Title = "Existing Movie",
            Genre = "Action",
            Price = 10m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Update(movie2.Id, dto);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_WithSameTitle_AllowsSelfUpdate()
    {
        var movie = await SeedMovie("Keep Title");

        var dto = new UpdateMovieDto
        {
            Title = "Keep Title",
            Genre = "Action",
            Price = 15m, // Changed price
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG"
        };

        var result = await _controller.Update(movie.Id, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var updatedDto = Assert.IsType<MovieDto>(okResult.Value);
        Assert.Equal(movie.Id, updatedDto.Id);
        Assert.Equal(15m, updatedDto.Price);
    }

    // ── Integration scenarios ───────────────────────────────────

    [Fact]
    public async Task FullCrudRoundtrip_CreatesReadsUpdatesDeletes()
    {
        // Create
        var createDto = new CreateMovieDto
        {
            Title = "Roundtrip Movie",
            Genre = "Action",
            Price = 10.00m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };
        var createResult = await _controller.Create(createDto);
        var createdAtResult = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var createdDto = Assert.IsType<MovieDto>(createdAtResult.Value);
        var newId = createdDto.Id;

        // Read
        var readResult = await _controller.GetById(newId);
        var readOk = Assert.IsType<OkObjectResult>(readResult.Result);
        var readDto = Assert.IsType<MovieDto>(readOk.Value);
        Assert.Equal(createdDto.Title, readDto.Title);

        // Update
        var updateDto = new UpdateMovieDto
        {
            Title = "Roundtrip Updated",
            Genre = "Drama",
            Price = 15.00m,
            ReleaseDate = new DateOnly(2025, 1, 1),
            Rating = "R"
        };
        var updateResult = await _controller.Update(newId, updateDto);
        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updatedDto = Assert.IsType<MovieDto>(updateOk.Value);
        Assert.Equal("Roundtrip Updated", updatedDto.Title);
        Assert.Equal(newId, updatedDto.Id); // Same Id

        // Delete
        var deleteResult = await _controller.Delete(newId);
        Assert.IsType<NoContentResult>(deleteResult);

        // Verify deleted
        var getDeleted = await _controller.GetById(newId);
        Assert.IsType<NotFoundObjectResult>(getDeleted.Result);
    }

    [Fact]
    public async Task GetAll_AfterCreate_ReturnsNewMovie()
    {
        var dto = new CreateMovieDto
        {
            Title = "Newly Added",
            Genre = "Comedy",
            Price = 5.99m,
            ReleaseDate = new DateOnly(2024, 6, 1),
            Rating = "PG"
        };
        await _controller.Create(dto);

        var result = await _controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Contains(movies, m => m.Title == "Newly Added");
    }

    [Fact]
    public async Task GetAll_AfterDelete_ExcludesDeletedMovie()
    {
        await SeedMovie("Will Be Deleted");
        await SeedMovie("Stays");
        var toDelete = await _context.Movie.FirstAsync(m => m.Title == "Will Be Deleted");

        await _controller.Delete(toDelete.Id);

        var result = await _controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.DoesNotContain(movies, m => m.Title == "Will Be Deleted");
        Assert.Contains(movies, m => m.Title == "Stays");
    }
}
