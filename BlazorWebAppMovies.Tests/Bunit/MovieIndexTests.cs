using System.Net;
using System.Net.Http.Json;
using Bunit;
using BlazorWebAppMovies.Components.Pages.MoviePages;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for MoviePages/Index.razor — movie list, search, auth-gated buttons, lightbox.
/// </summary>
public class MovieIndexTests : BunitTestBase
{
    private readonly List<MovieDto> _sampleMovies;

    public MovieIndexTests()
    {
        _sampleMovies =
        [
            new() { Id = 1, Title = "Star Wars", Genre = "Sci-Fi", Price = 19.99m, ReleaseDate = new DateOnly(1977, 5, 25), Rating = "PG", PosterUrl = "https://example.com/sw.jpg" },
            new() { Id = 2, Title = "The Matrix", Genre = "Sci-Fi", Price = 14.99m, ReleaseDate = new DateOnly(1999, 3, 31), Rating = "R" },
            new() { Id = 3, Title = "Jaws", Genre = "Thriller", Price = 12.99m, ReleaseDate = new DateOnly(1975, 6, 20), Rating = "PG" },
        ];
    }

    private global::Bunit.IRenderedComponent<global::BlazorWebAppMovies.Components.Pages.MoviePages.Index> RenderIndex()
    {
        return Ctx.RenderComponent<global::BlazorWebAppMovies.Components.Pages.MoviePages.Index>();
    }

    [Fact]
    public void Index_RendersMoviesFromApi()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(false);

        var cut = RenderIndex();

        // Wait for the async OnInitializedAsync to complete
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Star Wars", cut);
        AssertContains("The Matrix", cut);
        AssertContains("Jaws", cut);
        AssertContains("Sci-Fi", cut);
        AssertContains("Thriller", cut);
    }

    [Fact]
    public void Index_ShowsSearchInput()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(false);

        var cut = RenderIndex();

        AssertElementExists("input[type='search']", cut);
        AssertContains("Search by title", cut);
    }

    [Fact]
    public void Index_ShowsCreateNewButton_WhenAuthenticated()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Create New", cut);
        AssertElementExists("a[href='movies/create']", cut);
    }

    [Fact]
    public void Index_HidesCreateNewButton_WhenNotAuthenticated()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(false);

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertNotContains("Create New", cut);
    }

    [Fact]
    public void Index_ShowsEditAndDeleteButtonsPerRow_WhenAuthenticated()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        // Check that edit and delete links exist for each movie row
        var editLinks = cut.FindAll("a.btn-warning");
        var deleteLinks = cut.FindAll("a.btn-outline-danger");

        Assert.Equal(3, editLinks.Count);
        Assert.Equal(3, deleteLinks.Count);
    }

    [Fact]
    public void Index_HidesEditAndDeleteButtons_WhenNotAuthenticated()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(false);

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertElementNotExists("a.btn-warning", cut);
        AssertElementNotExists("a.btn-outline-danger", cut);
    }

    [Fact]
    public void Index_ShowsDetailsButtonPerRow_RegardlessOfAuth()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(false);

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        var detailsLinks = cut.FindAll("a.btn-info");
        Assert.Equal(3, detailsLinks.Count);
    }

    [Fact]
    public void Index_ShowsEmptyTable_WhenNoMovies()
    {
        RespondJson(HttpMethod.Get, "/api/movies", new List<MovieDto>());
        SetAuthState(false);

        var cut = RenderIndex();

        // Wait for the async load to finish — search input is always rendered
        cut.WaitForElement("input[type='search']", timeout: TimeSpan.FromSeconds(3));

        // The QuickGrid renders a table with thead when items are loaded (even empty)
        AssertElementExists("table.table", cut);
    }

    [Fact]
    public void Index_DisplaysPosterThumbnail_WhenPosterUrlIsPresent()
    {
        RespondJson(HttpMethod.Get, "/api/movies", _sampleMovies);
        SetAuthState(false);

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        // Star Wars has a PosterUrl, so it should render an img in the poster column
        var posterImgs = cut.FindAll("img.movie-poster-thumb");
        Assert.Single(posterImgs);
        Assert.Equal("https://example.com/sw.jpg", posterImgs[0].GetAttribute("src"));
    }

    [Fact]
    public void Index_ShowsPlaceholder_WhenPosterUrlIsMissing()
    {
        // Only The Matrix (no PosterUrl) — Star Wars has a poster so no placeholder
        var movies = _sampleMovies.Skip(1).Take(1).ToList();
        RespondJson(HttpMethod.Get, "/api/movies", movies);
        SetAuthState(false);

        var cut = RenderIndex();
        cut.WaitForState(() => cut.Markup.Contains("The Matrix"), timeout: TimeSpan.FromSeconds(2));

        // Movies without PosterUrl get a placeholder div with 🎬
        var placeholders = cut.FindAll("div.movie-poster-thumb-placeholder");
        Assert.Single(placeholders);
    }
}
