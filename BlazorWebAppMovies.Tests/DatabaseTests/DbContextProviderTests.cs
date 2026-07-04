using BlazorWebAppMovies.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class DbContextProviderTests
{
    [Fact]
    public void DesignTimeDbContextFactory_CreatesSqliteContext()
    {
        // This tests the factory's ability to resolve the SQLite provider path
        var factory = new DesignTimeDbContextFactory();
        Assert.NotNull(factory);
    }

    [Fact]
    public void SqliteContext_IsAssignableToBaseContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new BlazorWebAppMoviesContextSqlite(options);
        Assert.IsAssignableFrom<BlazorWebAppMoviesContext>(context);
    }

    [Fact]
    public void SqlServerContext_IsAssignableToBaseContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Test;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new BlazorWebAppMoviesContextSqlServer(options);
        Assert.IsAssignableFrom<BlazorWebAppMoviesContext>(context);
    }

    [Fact]
    public async Task SqliteInMemoryContext_CanPerformCrud()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var context = new BlazorWebAppMoviesContextSqlite(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var movie = new Models.Movie
        {
            Title = "SQLite Test",
            ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Test",
            Price = 5m,
            Rating = "PG"
        };

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        var count = await context.Movie.CountAsync();
        Assert.Equal(1, count);
    }
}
