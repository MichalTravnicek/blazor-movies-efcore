using System.Net;
using Bunit;
using BlazorWebAppMovies.Components.Pages.MoviePages;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for MoviePages/Details.razor — movie details rendering, poster display, loading state.
/// </summary>
public class MovieDetailsTests : BunitTestBase
{
    private readonly MovieDto _sampleMovieWithPoster = new()
    {
        Id = 1,
        Title = "Star Wars",
        Genre = "Sci-Fi",
        Price = 19.99m,
        ReleaseDate = new DateOnly(1977, 5, 25),
        Rating = "PG",
        PosterUrl = "https://example.com/sw.jpg"
    };

    private readonly MovieDto _sampleMovieNoPoster = new()
    {
        Id = 2,
        Title = "The Matrix",
        Genre = "Sci-Fi",
        Price = 14.99m,
        ReleaseDate = new DateOnly(1999, 3, 31),
        Rating = "R"
    };

    [Fact]
    public void Details_ShowsLoadingThenResolves()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovieWithPoster);
        SetAuthState(false);
        NavigateTo("/movies/details?id=1");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Details_RendersMovieDetails_WhenLoaded()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovieWithPoster);
        SetAuthState(false);
        NavigateTo("/movies/details?id=1");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Star Wars", cut);
        AssertContains("Sci-Fi", cut);
        // Price renders as decimal; check either format
        var hasPrice = cut.Markup.Contains("19.99") || cut.Markup.Contains("19,99");
        Assert.True(hasPrice, "Price should be rendered");
        AssertContains("PG", cut);

        AssertContains("Title", cut);
        AssertContains("Release Date", cut);
        AssertContains("Genre", cut);
        AssertContains("Price", cut);
        AssertContains("Rating", cut);
    }

    [Fact]
    public void Details_ShowsPosterImage_WhenPosterUrlIsPresent()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovieWithPoster);
        SetAuthState(false);
        NavigateTo("/movies/details?id=1");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        var posterImg = cut.Find("img.movie-poster-detail");
        Assert.NotNull(posterImg);
        Assert.Equal("https://example.com/sw.jpg", posterImg.GetAttribute("src"));
    }

    [Fact]
    public void Details_ShowsNoPosterAvailable_WhenPosterUrlIsMissing()
    {
        RespondJson(HttpMethod.Get, "/api/movies/2", _sampleMovieNoPoster);
        SetAuthState(false);
        NavigateTo("/movies/details?id=2");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => cut.Markup.Contains("The Matrix"), timeout: TimeSpan.FromSeconds(2));

        AssertContains("No poster available", cut);
        AssertElementNotExists("img.movie-poster-detail", cut);
    }

    [Fact]
    public void Details_ShowsFetchPosterButton_WhenNoPoster()
    {
        RespondJson(HttpMethod.Get, "/api/movies/2", _sampleMovieNoPoster);
        SetAuthState(false);
        NavigateTo("/movies/details?id=2");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => cut.Markup.Contains("The Matrix"), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Fetch Poster", cut);
    }

    [Fact]
    public void Details_ShowsEditAndBackLinks()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovieWithPoster);
        SetAuthState(false);
        NavigateTo("/movies/details?id=1");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        var editLink = cut.Find("a[href*='movies/edit']");
        Assert.NotNull(editLink);
        Assert.Contains("Edit", editLink.TextContent);

        var backLink = cut.Find("a[href='/movies']");
        Assert.NotNull(backLink);
        Assert.Contains("Back to List", backLink.TextContent);
    }

    [Fact]
    public void Details_ShowsMovieNotFound_WhenMovieNotFound()
    {
        RespondEmpty(HttpMethod.Get, "/api/movies/999", HttpStatusCode.NotFound);
        SetAuthState(false);
        NavigateTo("/movies/details?id=999");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Movie not found.", cut);
    }

    [Fact]
    public void Details_ShowsMovieNotFound_OnApiError()
    {
        RespondEmpty(HttpMethod.Get, "/api/movies/1", HttpStatusCode.InternalServerError);
        SetAuthState(false);
        NavigateTo("/movies/details?id=1");

        var cut = Ctx.RenderComponent<Details>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Movie not found.", cut);
    }
}
