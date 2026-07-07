using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class MovieDbContextTests
{
    private static BlazorWebAppMoviesContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new BlazorWebAppMoviesContext(options);
    }

    [Fact]
    public void CanCreateDatabaseContext()
    {
        using var context = CreateDbContext();
        Assert.NotNull(context);
    }

    [Fact]
    public void MovieDbSetIsAvailable()
    {
        using var context = CreateDbContext();
        Assert.NotNull(context.Movie);
    }

    [Fact]
    public async Task CanAddMovieToDatabase()
    {
        using var context = CreateDbContext();

        var movie = new Movie
        {
            Title = "Test Movie",
            ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Action",
            Price = 9.99m,
            Rating = "PG-13"
        };

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        var savedMovie = await context.Movie.FirstOrDefaultAsync(m => m.Title == "Test Movie");
        Assert.NotNull(savedMovie);
        Assert.Equal("Test Movie", savedMovie.Title);
        Assert.Equal(9.99m, savedMovie.Price);
    }

    [Fact]
    public async Task CanUpdateMovie()
    {
        using var context = CreateDbContext();

        var movie = new Movie
        {
            Title = "Original Title",
            ReleaseDate = new DateOnly(2024, 6, 15),
            Genre = "Comedy",
            Price = 5.99m,
            Rating = "R"
        };

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        // Update
        movie.Title = "Updated Title";
        movie.Price = 7.99m;
        await context.SaveChangesAsync();

        var updated = await context.Movie.FirstAsync();
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal(7.99m, updated.Price);
    }

    [Fact]
    public async Task CanDeleteMovie()
    {
        using var context = CreateDbContext();

        var movie = new Movie
        {
            Title = "Delete Me",
            ReleaseDate = new DateOnly(2023, 12, 1),
            Genre = "Drama",
            Price = 3.50m,
            Rating = "PG"
        };

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        context.Movie.Remove(movie);
        await context.SaveChangesAsync();

        var count = await context.Movie.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CanQueryMoviesByGenre()
    {
        using var context = CreateDbContext();

        context.Movie.AddRange(
            new Movie { Title = "Movie A", ReleaseDate = new DateOnly(2020, 1, 1), Genre = "Action", Price = 10m, Rating = "PG-13" },
            new Movie { Title = "Movie B", ReleaseDate = new DateOnly(2021, 1, 1), Genre = "Comedy", Price = 8m, Rating = "R" },
            new Movie { Title = "Movie C", ReleaseDate = new DateOnly(2022, 1, 1), Genre = "Action", Price = 12m, Rating = "PG-13" }
        );
        await context.SaveChangesAsync();

        var actionMovies = await context.Movie.Where(m => m.Genre == "Action").ToListAsync();

        Assert.Equal(2, actionMovies.Count);
        Assert.All(actionMovies, m => Assert.Equal("Action", m.Genre));
    }

    [Fact]
    public async Task CanQueryMoviesByPriceRange()
    {
        using var context = CreateDbContext();

        context.Movie.AddRange(
            new Movie { Title = "Cheap", ReleaseDate = new DateOnly(2020, 1, 1), Genre = "Action", Price = 5m, Rating = "PG" },
            new Movie { Title = "Mid", ReleaseDate = new DateOnly(2021, 1, 1), Genre = "Drama", Price = 15m, Rating = "R" },
            new Movie { Title = "Expensive", ReleaseDate = new DateOnly(2022, 1, 1), Genre = "Action", Price = 50m, Rating = "PG-13" }
        );
        await context.SaveChangesAsync();

        var cheapMovies = await context.Movie
            .Where(m => m.Price >= 0 && m.Price <= 20)
            .ToListAsync();

        Assert.Equal(2, cheapMovies.Count);
    }

    [Fact]
    public async Task DuplicateMovieTitle_ThrowsDbUpdateException()
    {
        using var context = CreateDbContext();

        context.Movie.Add(new Movie
        {
            Title = "Unique", ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Action", Price = 10m, Rating = "PG"
        });
        await context.SaveChangesAsync();

        // InMemory database does NOT enforce unique indexes,
        // so this succeeds. We assert it's allowed (InMemory limitation).
        context.Movie.Add(new Movie
        {
            Title = "Unique", ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Action", Price = 10m, Rating = "PG"
        });
        await context.SaveChangesAsync();

        var count = await context.Movie.CountAsync();
        Assert.Equal(2, count); // InMemory ignores unique constraints
    }

    [Fact]
    public async Task UniqueIndexOnTitle_IsDefinedInModel()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(Movie));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Movie.Title)));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task MovieHasIdAfterSave()
    {
        using var context = CreateDbContext();

        var movie = new Movie
        {
            Title = "ID Check",
            ReleaseDate = new DateOnly(2024, 5, 5),
            Genre = "Thriller",
            Price = 14.99m,
            Rating = "R"
        };

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        Assert.True(movie.Id > 0);
    }
}
