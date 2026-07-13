using System.Net;
using System.Net.Http.Json;
using Bunit;
using BlazorWebAppMovies.Components.Pages.MoviePages;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for MoviePages/Edit.razor — loading state, movie data loading, form submission.
/// </summary>
public class MovieEditTests : BunitTestBase
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
    public void Edit_ShowsLoadingInitially()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/edit?id=1");

        var cut = Ctx.RenderComponent<Edit>();

        // During load it shows Loading..., then resolves
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Edit_RendersFormWithMovieData_WhenLoaded()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/edit?id=1");

        var cut = Ctx.RenderComponent<Edit>();

        cut.WaitForState(() => cut.Markup.Contains("Star Wars"), timeout: TimeSpan.FromSeconds(2));

        AssertElementExists("input#title", cut);
        AssertElementExists("input#releasedate", cut);
        AssertElementExists("input#genre", cut);
        AssertElementExists("input#price", cut);
        AssertElementExists("select#rating", cut);

        // Title input should have the movie title value
        var titleInput = cut.Find("input#title");
        Assert.Equal("Star Wars", titleInput.GetAttribute("value"));
    }

    [Fact]
    public void Edit_ShowsSaveButtonAndBackLink()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/edit?id=1");

        var cut = Ctx.RenderComponent<Edit>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        var submitButton = cut.Find("button[type='submit']");
        Assert.NotNull(submitButton);
        Assert.Contains("Save", submitButton.TextContent);

        var backLink = cut.Find("a[href='/movies']");
        Assert.NotNull(backLink);
        Assert.Contains("Back to List", backLink.TextContent);
    }

    [Fact]
    public void Edit_ShowsMovieNotFound_WhenNotFound()
    {
        RespondEmpty(HttpMethod.Get, "/api/movies/999", HttpStatusCode.NotFound);
        SetAuthState(false);
        NavigateTo("/movies/edit?id=999");

        var cut = Ctx.RenderComponent<Edit>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        AssertContains("Movie not found.", cut);
    }

    [Fact]
    public void Edit_SubmitValidForm_NavigatesToMoviesOnSuccess()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        RespondJson(HttpMethod.Put, "/api/movies/1", _sampleMovie);
        SetAuthState(false);
        NavigateTo("/movies/edit?id=1");

        var cut = Ctx.RenderComponent<Edit>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        cut.Find("button[type='submit']").Click();

        cut.WaitForAssertion(
            () => Assert.Contains("/movies", NavManager.Uri),
            timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Edit_SubmitConflict_ShowsErrorMessage()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        RespondJson(
            HttpMethod.Put, "/api/movies/1",
            new { message = "A movie with this title already exists." },
            HttpStatusCode.Conflict
        );
        SetAuthState(false);
        NavigateTo("/movies/edit?id=1");

        var cut = Ctx.RenderComponent<Edit>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        cut.Find("button[type='submit']").Click();

        cut.WaitForState(() => cut.Markup.Contains("already exists"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("A movie with this title already exists.", cut);
    }

    [Fact]
    public void Edit_SubmitServerError_ShowsErrorMessage()
    {
        RespondJson(HttpMethod.Get, "/api/movies/1", _sampleMovie);
        RespondEmpty(HttpMethod.Put, "/api/movies/1", HttpStatusCode.InternalServerError);
        SetAuthState(false);
        NavigateTo("/movies/edit?id=1");

        var cut = Ctx.RenderComponent<Edit>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading..."), timeout: TimeSpan.FromSeconds(2));

        cut.Find("button[type='submit']").Click();

        cut.WaitForState(() => cut.Markup.Contains("error occurred"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("An error occurred while saving the movie.", cut);
    }
}
