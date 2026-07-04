using BlazorWebAppMovies.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class SeedDataTests
{
    private static IDbContextFactory<BlazorWebAppMoviesContext> CreateDbContextFactory(string? dbName = null)
    {
        var services = new ServiceCollection();

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString()));

        return services.BuildServiceProvider()
            .GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();
    }

    [Fact]
    public void SeedData_SeedsMovies_WhenDatabaseEmpty()
    {
        var factory = CreateDbContextFactory();

        SeedData.Initialize(factory);

        using var context = factory.CreateDbContext();
        var movies = context.Movie.ToList();
        Assert.NotEmpty(movies);
    }

    [Fact]
    public void SeedData_DoesNotDuplicate_WhenAlreadySeeded()
    {
        var factory = CreateDbContextFactory();

        // First call seeds
        SeedData.Initialize(factory);
        // Second call should not duplicate
        SeedData.Initialize(factory);

        using var context = factory.CreateDbContext();

        // Assert that Mad Max appears only once (unique by title check)
        var madMaxCount = context.Movie.Count(m => m.Title == "Mad Max");
        Assert.Equal(1, madMaxCount);
    }

    [Fact]
    public void SeedData_ContainsExpectedMovies()
    {
        var factory = CreateDbContextFactory();

        SeedData.Initialize(factory);

        using var context = factory.CreateDbContext();
        var movies = context.Movie.ToList();

        Assert.Contains(movies, m => m.Title == "Mad Max");
        Assert.Contains(movies, m => m.Title == "The Road Warrior");
        Assert.Contains(movies, m => m.Title == "Mad Max: Beyond Thunderdome");
        Assert.Contains(movies, m => m.Title == "Mad Max: Fury Road");
        Assert.Contains(movies, m => m.Title == "Furiosa: A Mad Max Saga");
        Assert.Equal(5, movies.Count);
    }

    [Fact]
    public void SeedData_MoviesHaveValidData()
    {
        var factory = CreateDbContextFactory();

        SeedData.Initialize(factory);

        using var context = factory.CreateDbContext();
        var furyRoad = context.Movie.First(m => m.Title == "Mad Max: Fury Road");

        Assert.Equal(new DateOnly(2015, 5, 15), furyRoad.ReleaseDate);
        Assert.Equal("Sci-fi (Cyberpunk)", furyRoad.Genre);
        Assert.Equal(8.43m, furyRoad.Price);
        Assert.Equal("R", furyRoad.Rating);
    }
}