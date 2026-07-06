using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class SeedDataTests
{
    private static (IDbContextFactory<BlazorWebAppMoviesContext>, IServiceProvider) CreateServices(string? dbName = null)
    {
        var dbNameResolved = dbName ?? Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbNameResolved));

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbNameResolved));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        return (factory, serviceProvider);
    }

    [Fact]
    public async Task SeedData_SeedsMovies_WhenDatabaseEmpty()
    {
        var (factory, services) = CreateServices();

        await SeedData.Initialize(factory, services);

        using var context = factory.CreateDbContext();
        var movies = context.Movie.ToList();
        Assert.NotEmpty(movies);
    }

    [Fact]
    public async Task SeedData_DoesNotDuplicate_WhenAlreadySeeded()
    {
        var (factory, services) = CreateServices();

        // First call seeds
        await SeedData.Initialize(factory, services);
        // Second call should not duplicate
        await SeedData.Initialize(factory, services);

        using var context = factory.CreateDbContext();

        // Assert that Mad Max appears only once
        var madMaxCount = context.Movie.Count(m => m.Title == "Mad Max");
        Assert.Equal(1, madMaxCount);
    }

    [Fact]
    public async Task SeedData_ContainsExpectedMovies()
    {
        var (factory, services) = CreateServices();

        await SeedData.Initialize(factory, services);

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
    public async Task SeedData_MoviesHaveValidData()
    {
        var (factory, services) = CreateServices();

        await SeedData.Initialize(factory, services);

        using var context = factory.CreateDbContext();
        var furyRoad = context.Movie.First(m => m.Title == "Mad Max: Fury Road");

        Assert.Equal(new DateOnly(2015, 5, 15), furyRoad.ReleaseDate);
        Assert.Equal("Sci-fi (Cyberpunk)", furyRoad.Genre);
        Assert.Equal(8.43m, furyRoad.Price);
        Assert.Equal("R", furyRoad.Rating);
    }
}
