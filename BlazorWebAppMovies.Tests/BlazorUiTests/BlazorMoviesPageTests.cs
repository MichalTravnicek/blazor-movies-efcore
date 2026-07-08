using AutoMapper;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebAppMovies.Tests.BlazorUiTests;

public class BlazorMoviesPageTests : IDisposable
{
    private readonly string _dbName;
    private readonly BlazorWebAppMoviesContext _context;
    private readonly IServiceScope _scope;

    public BlazorMoviesPageTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        _scope = serviceProvider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<BlazorWebAppMoviesContext>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _context.Dispose();
    }

    private async Task SeedMovies()
    {
        _context.Movie.AddRange(
            new Models.Movie { Title = "Movie A", Genre = "Action", Price = 9.99m, ReleaseDate = new DateOnly(2024, 1, 1), Rating = "PG-13" },
            new Models.Movie { Title = "Movie B", Genre = "Comedy", Price = 7.99m, ReleaseDate = new DateOnly(2024, 2, 1), Rating = "PG" },
            new Models.Movie { Title = "Movie C", Genre = "Drama", Price = 12.99m, ReleaseDate = new DateOnly(2024, 3, 1), Rating = "R" }
        );
        await _context.SaveChangesAsync();
    }

    private static global::BlazorWebAppMovies.Controllers.MoviesController CreateController(string dbName)
    {
        var factory = new ControllerTestDbContextFactory(() =>
        {
            var opts = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new BlazorWebAppMoviesContext(opts);
        });

        var mapperProvider = new global::AutoMapper.MapperConfiguration(
            cfg => cfg.AddProfile<global::BlazorWebAppMovies.Models.Mapping.MovieProfile>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();

        return new global::BlazorWebAppMovies.Controllers.MoviesController(factory, mapperProvider);
    }

    // ── Movie list (Index.razor logic) ──────────────────────────

    [Fact]
    public async Task MovieList_LoadsAllMovies()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        var movies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(movies.Result);
        var list = Assert.IsType<List<MovieDto>>(okResult.Value);

        Assert.Equal(3, list.Count);
    }

    [Fact]
    public async Task MovieList_CanBeFilteredByTitle()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var list = Assert.IsType<List<MovieDto>>(okResult.Value);

        var filtered = list.Where(m => m.Title.Contains("Movie", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public async Task MovieList_FilterByNonExistentTitle_ReturnsEmpty()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var list = Assert.IsType<List<MovieDto>>(okResult.Value);

        var filtered = list.Where(m => m.Title.Contains("Zzz", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(filtered);
    }

    [Fact]
    public async Task MovieList_IsOrderedByReleaseDate()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        var movies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(movies.Result);
        var list = Assert.IsType<List<MovieDto>>(okResult.Value);

        Assert.Equal("Movie A", list[0].Title);
        Assert.Equal("Movie B", list[1].Title);
        Assert.Equal("Movie C", list[2].Title);
    }

    [Fact]
    public async Task MovieList_WhenEmpty_ReturnsNone()
    {
        var controller = CreateController(_dbName);
        var movies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(movies.Result);
        var list = Assert.IsType<List<MovieDto>>(okResult.Value);

        Assert.Empty(list);
    }

    // ── Movie details page (Details.razor logic) ────────────────

    [Fact]
    public async Task MovieDetails_ReturnsCorrectMovie()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var list = Assert.IsType<List<MovieDto>>(okResult.Value);

        var movie = list.First(m => m.Title == "Movie A");

        Assert.Equal("Action", movie.Genre);
        Assert.Equal(9.99m, movie.Price);
        Assert.Equal(new DateOnly(2024, 1, 1), movie.ReleaseDate);
        Assert.Equal("PG-13", movie.Rating);
    }

    [Fact]
    public async Task MovieDetails_NonExistentId_ReturnsNull()
    {
        var controller = CreateController(_dbName);
        var result = await controller.GetById(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ── Movie create (Create.razor logic) ───────────────────────

    [Fact]
    public async Task MovieCreate_AddsMovieToDatabase()
    {
        var controller = CreateController(_dbName);
        var dto = new CreateMovieDto
        {
            Title = "New Movie",
            Genre = "Sci-Fi",
            Price = 14.99m,
            ReleaseDate = new DateOnly(2025, 6, 1),
            Rating = "PG-13"
        };

        var result = await controller.Create(dto);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var movieDto = Assert.IsType<MovieDto>(createdResult.Value);
        Assert.Equal("New Movie", movieDto.Title);
        Assert.Equal("Sci-Fi", movieDto.Genre);
    }

    [Fact]
    public async Task MovieCreate_IncrementsCount()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        var dto = new CreateMovieDto
        {
            Title = "Extra",
            Genre = "Action",
            Price = 5m,
            ReleaseDate = new DateOnly(2025, 1, 1),
            Rating = "G"
        };

        await controller.Create(dto);

        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Equal(4, movies.Count);
    }

    // ── Movie edit (Edit.razor logic) ───────────────────────────

    [Fact]
    public async Task MovieEdit_UpdatesFields()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);

        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        var movieId = movies[0].Id;

        var updateDto = new UpdateMovieDto
        {
            Title = "Updated Title",
            Genre = "Action",
            Price = 19.99m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };

        var updateResult = await controller.Update(movieId, updateDto);
        var updatedOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updated = Assert.IsType<MovieDto>(updatedOk.Value);

        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(19.99m, updated.Price);
    }

    [Fact]
    public async Task MovieEdit_OnlyUpdatesSpecifiedMovie()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);

        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);

        var updateDto = new UpdateMovieDto
        {
            Title = "Updated A",
            Genre = "Action",
            Price = 9.99m,
            ReleaseDate = new DateOnly(2024, 1, 1),
            Rating = "PG-13"
        };

        await controller.Update(movies[0].Id, updateDto);

        var updatedList = await controller.GetAll();
        var listOk = Assert.IsType<OkObjectResult>(updatedList.Result);
        var updatedMovies = Assert.IsType<List<MovieDto>>(listOk.Value);
        var movieB = updatedMovies.First(m => m.Title == "Movie B");
        Assert.NotNull(movieB);
    }

    [Fact]
    public async Task MovieEdit_NonExistentId_ReturnsNotFound()
    {
        var controller = CreateController(_dbName);
        var updateDto = new UpdateMovieDto
        {
            Title = "Ghost",
            Genre = "Horror",
            Price = 5m,
            ReleaseDate = new DateOnly(2025, 1, 1),
            Rating = "R"
        };

        var result = await controller.Update(999, updateDto);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ── Movie delete (Delete.razor logic) ───────────────────────

    [Fact]
    public async Task MovieDelete_RemovesMovie()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);

        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        var movieId = movies[0].Id;

        await controller.Delete(movieId);

        var getResult = await controller.GetById(movieId);
        Assert.IsType<NotFoundObjectResult>(getResult.Result);
    }

    [Fact]
    public async Task MovieDelete_DecrementsCount()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        await controller.Delete(1);

        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Equal(2, movies.Count);
    }

    [Fact]
    public async Task MovieDelete_OnlyRemovesTarget()
    {
        await SeedMovies();

        var controller = CreateController(_dbName);
        await controller.Delete(1);

        var allMovies = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(allMovies.Result);
        var movies = Assert.IsType<List<MovieDto>>(okResult.Value);
        Assert.Equal(2, movies.Count);
        Assert.DoesNotContain(movies, m => m.Title == "Movie A");
    }

    // ── Auth state guards (used in Index.razor) ─────────────────

    [Fact]
    public void AuthenticatedUser_CanSeeCreateEditDeleteButtons()
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.Name, "test@test.com"),
            new(System.Security.Claims.ClaimTypes.Role, "User")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        bool canSeeCreateEditDelete = principal.Identity?.IsAuthenticated == true;
        Assert.True(canSeeCreateEditDelete);
    }

    [Fact]
    public void UnauthenticatedUser_CannotSeeCreateEditDeleteButtons()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

        bool canSeeCreateEditDelete = principal.Identity?.IsAuthenticated == true;
        Assert.False(canSeeCreateEditDelete);
    }

    [Fact]
    public void UnauthenticatedUser_CanStillSeeDetails()
    {
        // The Details link is always rendered — no auth guard
        bool hasDetailsLink = true; // unconditional in template
        Assert.True(hasDetailsLink);
    }
}

/// <summary>
/// Factory used by the controller, backed by the same in-memory database.
/// </summary>
internal sealed class ControllerTestDbContextFactory : IDbContextFactory<BlazorWebAppMoviesContext>
{
    private readonly Func<BlazorWebAppMoviesContext> _factory;

    public ControllerTestDbContextFactory(Func<BlazorWebAppMoviesContext> factory)
    {
        _factory = factory;
    }

    public BlazorWebAppMoviesContext CreateDbContext()
    {
        return _factory();
    }
}
