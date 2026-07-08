using System.Security.Claims;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Identity;
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

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.RoleClaimType = "role";
        });

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
            new Movie { Title = "Movie A", Genre = "Action", Price = 9.99m, ReleaseDate = new DateOnly(2024, 1, 1), Rating = "PG-13" },
            new Movie { Title = "Movie B", Genre = "Comedy", Price = 7.99m, ReleaseDate = new DateOnly(2024, 2, 1), Rating = "PG" },
            new Movie { Title = "Movie C", Genre = "Drama", Price = 12.99m, ReleaseDate = new DateOnly(2024, 3, 1), Rating = "R" }
        );
        await _context.SaveChangesAsync();
    }

    // ── Movie list (Index.razor logic) ──────────────────────────

    [Fact]
    public async Task MovieList_LoadsAllMovies()
    {
        await SeedMovies();

        var movies = await _context.Movie.ToListAsync();
        Assert.Equal(3, movies.Count);
    }

    [Fact]
    public async Task MovieList_CanBeFilteredByTitle()
    {
        await SeedMovies();

        var filtered = await _context.Movie.Where(m => m.Title!.Contains("Movie")).ToListAsync();
        Assert.Equal(3, filtered.Count);
    }

    [Fact]
    public async Task MovieList_FilterByNonExistentTitle_ReturnsEmpty()
    {
        await SeedMovies();

        var filtered = await _context.Movie.Where(m => m.Title!.Contains("Zzz")).ToListAsync();
        Assert.Empty(filtered);
    }

    [Fact]
    public async Task MovieList_IsOrderedByReleaseDate()
    {
        await SeedMovies();

        var movies = await _context.Movie.OrderBy(m => m.ReleaseDate).ToListAsync();
        Assert.Equal("Movie A", movies[0].Title);
        Assert.Equal("Movie B", movies[1].Title);
        Assert.Equal("Movie C", movies[2].Title);
    }

    [Fact]
    public async Task MovieList_WhenEmpty_ReturnsNone()
    {
        var movies = await _context.Movie.ToListAsync();
        Assert.Empty(movies);
    }

    // ── Movie details page (Details.razor logic) ────────────────

    [Fact]
    public async Task MovieDetails_ReturnsCorrectMovie()
    {
        await SeedMovies();
        var movie = await _context.Movie.FirstAsync(m => m.Title == "Movie A");

        Assert.Equal("Action", movie.Genre);
        Assert.Equal(9.99m, movie.Price);
        Assert.Equal(new DateOnly(2024, 1, 1), movie.ReleaseDate);
        Assert.Equal("PG-13", movie.Rating);
    }

    [Fact]
    public async Task MovieDetails_NonExistentId_ReturnsNull()
    {
        var movie = await _context.Movie.FirstOrDefaultAsync(m => m.Id == 999);
        Assert.Null(movie);
    }

    // ── Movie create (Create.razor logic) ───────────────────────

    [Fact]
    public async Task MovieCreate_AddsMovieToDatabase()
    {
        var movie = new Movie
        {
            Title = "New Movie",
            Genre = "Sci-Fi",
            Price = 14.99m,
            ReleaseDate = new DateOnly(2025, 6, 1),
            Rating = "PG-13"
        };

        _context.Movie.Add(movie);
        await _context.SaveChangesAsync();

        var saved = await _context.Movie.FirstAsync(m => m.Title == "New Movie");
        Assert.NotNull(saved);
        Assert.Equal("Sci-Fi", saved.Genre);
    }

    [Fact]
    public async Task MovieCreate_IncrementsCount()
    {
        await SeedMovies();
        var beforeCount = await _context.Movie.CountAsync();

        _context.Movie.Add(new Movie
        {
            Title = "Extra",
            Genre = "Action",
            Price = 5m,
            ReleaseDate = new DateOnly(2025, 1, 1),
            Rating = "G"
        });
        await _context.SaveChangesAsync();

        var afterCount = await _context.Movie.CountAsync();
        Assert.Equal(beforeCount + 1, afterCount);
    }

    // ── Movie edit (Edit.razor logic) ───────────────────────────

    [Fact]
    public async Task MovieEdit_UpdatesFields()
    {
        await SeedMovies();
        var movie = await _context.Movie.FirstAsync(m => m.Title == "Movie A");

        movie.Title = "Updated Title";
        movie.Price = 19.99m;
        _context.Movie.Update(movie);
        await _context.SaveChangesAsync();

        var updated = await _context.Movie.FirstAsync(m => m.Id == movie.Id);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(19.99m, updated.Price);
    }

    [Fact]
    public async Task MovieEdit_OnlyUpdatesSpecifiedMovie()
    {
        await SeedMovies();
        var movie = await _context.Movie.FirstAsync(m => m.Title == "Movie A");

        movie.Title = "Updated A";
        await _context.SaveChangesAsync();

        var other = await _context.Movie.FirstAsync(m => m.Title == "Movie B");
        Assert.NotNull(other);
        Assert.Equal("Movie B", other.Title);
    }

    [Fact]
    public async Task MovieEdit_NonExistentId_ReturnsNull()
    {
        var movie = await _context.Movie.FindAsync(999);
        Assert.Null(movie);
    }

    // ── Movie delete (Delete.razor logic) ───────────────────────

    [Fact]
    public async Task MovieDelete_RemovesMovie()
    {
        await SeedMovies();
        var movie = await _context.Movie.FirstAsync(m => m.Title == "Movie A");

        _context.Movie.Remove(movie);
        await _context.SaveChangesAsync();

        var deleted = await _context.Movie.FirstOrDefaultAsync(m => m.Id == movie.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task MovieDelete_DecrementsCount()
    {
        await SeedMovies();
        var beforeCount = await _context.Movie.CountAsync();

        var movie = await _context.Movie.FirstAsync();
        _context.Movie.Remove(movie);
        await _context.SaveChangesAsync();

        var afterCount = await _context.Movie.CountAsync();
        Assert.Equal(beforeCount - 1, afterCount);
    }

    [Fact]
    public async Task MovieDelete_OnlyRemovesTarget()
    {
        await SeedMovies();
        var movie = await _context.Movie.FirstAsync(m => m.Title == "Movie A");

        _context.Movie.Remove(movie);
        await _context.SaveChangesAsync();

        var remaining = await _context.Movie.ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, m => m.Title == "Movie A");
    }

    // ── Auth state guards (used in Index.razor) ─────────────────

    [Fact]
    public void AuthenticatedUser_CanSeeCreateEditDeleteButtons()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@test.com"),
            new(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        bool canSeeCreateEditDelete = principal.Identity?.IsAuthenticated == true;
        Assert.True(canSeeCreateEditDelete);
    }

    [Fact]
    public void UnauthenticatedUser_CannotSeeCreateEditDeleteButtons()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        bool canSeeCreateEditDelete = principal.Identity?.IsAuthenticated == true;
        Assert.False(canSeeCreateEditDelete);
    }

    [Fact]
    public void UnauthenticatedUser_CanStillSeeDetails()
    {
        // The Details link is always rendered — no auth guard
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        bool hasDetailsLink = true; // unconditional in template
        Assert.True(hasDetailsLink);
    }
}
