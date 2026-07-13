using System.Net;
using Bunit;
using BlazorWebAppMovies.Components.Pages.MoviePages;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for MoviePages/Delete.razor — movie display, delete confirmation, navigation.
/// </summary>
public class MovieDeleteTests : BunitTestBase
{
    private readonly MovieDto _sampleMovie = new()
    {
        Id = 1,
        Title = "Star Wars",
        Genre = "Sci-Fi",
        Price = 19.99m,
        ReleaseDate = new DateOnly(1977, 5, 25),
        Rating = "PG"
    };

    [Fact]
    public void Delete_ShowsLoadingThenResolves()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/delete?id=1");

        var cut = Ctx.RenderComponent<Delete>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Delete_RendersMovieDetails_WhenLoaded()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/delete?id=1");

        var cut = Ctx.RenderComponent<Delete>();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Star Wars", cut);
        AssertContains("Sci-Fi", cut);
        AssertContains("PG", cut);
        AssertContains("Are you sure you want to delete this?", cut);

        // Price renders as decimal; check it's present somewhere in the markup
        var hasPrice = cut.Markup.Contains("19.99") || cut.Markup.Contains("19,99");
        Assert.True(hasPrice, "Price should be rendered");
    }

    [Fact]
    public void Delete_ShowsDeleteButtonAndBackLink()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/delete?id=1");

        var cut = Ctx.RenderComponent<Delete>();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        var submitButton = cut.Find("button[type='submit']");
        Assert.NotNull(submitButton);
        Assert.Contains("Delete", submitButton.TextContent);

        var backLink = cut.Find("a[href='/movies']");
        Assert.NotNull(backLink);
        Assert.Contains("Back to List", backLink.TextContent);
    }

    [Fact]
    public void Delete_SubmitDelete_NavigatesToMovies()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        RespondEmpty(HttpMethod.Delete, "/api/movies/1");
        SetAuthState(false);
        NavigateTo("/movies/delete?id=1");

        var cut = Ctx.RenderComponent<Delete>();
        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        cut.Find("button[type='submit']").Click();

        cut.WaitForAssertion(
            () => Assert.Contains("/movies", NavManager.Uri),
            timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Delete_ShowsMovieNotFound_WhenNotFound()
    {
        RespondEmpty(HttpMethod.Get, "/api/movies/999", HttpStatusCode.NotFound);
        SetAuthState(false);
        NavigateTo("/movies/delete?id=999");

        var cut = Ctx.RenderComponent<Delete>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Movie not found.", cut);
    }

    [Fact]
    public void Delete_ShowsMovieNotFound_OnApiError()
    {
        RespondEmpty(HttpMethod.Get, "/api/movies/1", HttpStatusCode.InternalServerError);
        SetAuthState(false);
        NavigateTo("/movies/delete?id=1");

        var cut = Ctx.RenderComponent<Delete>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Movie not found.", cut);
    }
}
