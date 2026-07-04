using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class MovieQueriesTests
{
    private static async Task<BlazorWebAppMoviesContext> GetSeededContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new BlazorWebAppMoviesContext(options);

        context.Movie.AddRange(
            new Movie { Title = "Mad Max", ReleaseDate = new DateOnly(1979, 4, 12), Genre = "Sci-fi (Cyberpunk)", Price = 2.51m, Rating = "R" },
            new Movie { Title = "The Road Warrior", ReleaseDate = new DateOnly(1981, 12, 24), Genre = "Sci-fi (Cyberpunk)", Price = 2.78m, Rating = "R" },
            new Movie { Title = "Mad Max: Beyond Thunderdome", ReleaseDate = new DateOnly(1985, 7, 10), Genre = "Sci-fi (Cyberpunk)", Price = 3.55m, Rating = "PG-13" },
            new Movie { Title = "Mad Max: Fury Road", ReleaseDate = new DateOnly(2015, 5, 15), Genre = "Sci-fi (Cyberpunk)", Price = 8.43m, Rating = "R" },
            new Movie { Title = "Furiosa: A Mad Max Saga", ReleaseDate = new DateOnly(2024, 5, 24), Genre = "Sci-fi (Cyberpunk)", Price = 13.49m, Rating = "R" }
        );

        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task GetAllMovies_ReturnsAllSeedMovies()
    {
        using var context = await GetSeededContext();
        var movies = await context.Movie.ToListAsync();

        Assert.Equal(5, movies.Count);
    }

    [Fact]
    public async Task GetMovieById_ReturnsCorrectMovie()
    {
        using var context = await GetSeededContext();
        var movie = await context.Movie.FirstAsync(m => m.Title == "Mad Max");

        var found = await context.Movie.FindAsync(movie.Id);

        Assert.NotNull(found);
        Assert.Equal("Mad Max", found.Title);
    }

    [Fact]
    public async Task SearchMoviesByTitle_ReturnsFilteredResults()
    {
        using var context = await GetSeededContext();

        var results = await context.Movie
            .Where(m => m.Title!.Contains("Fury"))
            .ToListAsync();

        Assert.Single(results);
        Assert.Equal("Mad Max: Fury Road", results[0].Title);
    }

    [Fact]
    public async Task GetMoviesByRating_ReturnsCorrectCount()
    {
        using var context = await GetSeededContext();

        var rRated = await context.Movie
            .Where(m => m.Rating == "R")
            .ToListAsync();

        var pg13Rated = await context.Movie
            .Where(m => m.Rating == "PG-13")
            .ToListAsync();

        Assert.Equal(4, rRated.Count);
        Assert.Single(pg13Rated);
    }

    [Fact]
    public async Task OrderMoviesByPrice_ReturnsAscending()
    {
        using var context = await GetSeededContext();

        var movies = await context.Movie
            .OrderBy(m => m.Price)
            .ToListAsync();

        Assert.Equal(2.51m, movies[0].Price);
        Assert.Equal(13.49m, movies[^1].Price);
    }

    [Fact]
    public async Task GetMoviesReleasedAfterDate_ReturnsFiltered()
    {
        using var context = await GetSeededContext();

        var modernMovies = await context.Movie
            .Where(m => m.ReleaseDate > new DateOnly(2000, 1, 1))
            .ToListAsync();

        Assert.Equal(2, modernMovies.Count);
        Assert.Contains(modernMovies, m => m.Title == "Mad Max: Fury Road");
        Assert.Contains(modernMovies, m => m.Title == "Furiosa: A Mad Max Saga");
    }

    [Fact]
    public async Task PriceTotal_CalculatesCorrectSum()
    {
        using var context = await GetSeededContext();

        var totalPrice = await context.Movie.SumAsync(m => m.Price);

        // 2.51 + 2.78 + 3.55 + 8.43 + 13.49
        Assert.Equal(30.76m, totalPrice);
    }
}
