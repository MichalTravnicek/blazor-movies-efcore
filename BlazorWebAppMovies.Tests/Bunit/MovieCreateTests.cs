using System.Net;
using Bunit;
using BlazorWebAppMovies.Components.Pages.MoviePages;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for MoviePages/Create.razor — form rendering, validation, submit flow.
/// </summary>
public class MovieCreateTests : BunitTestBase
{
    [Fact]
    public void Create_RendersFormWithAllFields()
    {
        var cut = Ctx.RenderComponent<Create>();

        AssertContains("Create", cut);
        AssertContains("Title", cut);
        AssertContains("Release Date", cut);
        AssertContains("Genre", cut);
        AssertContains("Price", cut);
        AssertContains("Rating", cut);

        AssertElementExists("input#title", cut);
        AssertElementExists("input#releasedate", cut);
        AssertElementExists("input#genre", cut);
        AssertElementExists("input#price", cut);
        AssertElementExists("select#rating", cut);
    }

    [Fact]
    public void Create_HasAllRatingOptions()
    {
        var cut = Ctx.RenderComponent<Create>();

        var select = cut.Find("select#rating");
        var options = select.Children;

        Assert.Contains(options, o => o.TextContent.Trim() == "G");
        Assert.Contains(options, o => o.TextContent.Trim() == "PG");
        Assert.Contains(options, o => o.TextContent.Trim() == "PG-13");
        Assert.Contains(options, o => o.TextContent.Trim() == "R");
        Assert.Contains(options, o => o.TextContent.Trim() == "NC-17");
    }

    [Fact]
    public void Create_HasSubmitButtonAndBackLink()
    {
        var cut = Ctx.RenderComponent<Create>();

        var submitButton = cut.Find("button[type='submit']");
        Assert.NotNull(submitButton);
        Assert.Contains("Create", submitButton.TextContent);

        var backLink = cut.Find("a[href='/movies']");
        Assert.NotNull(backLink);
        Assert.Contains("Back to List", backLink.TextContent);
    }

    [Fact]
    public void Create_SubmitValidForm_NavigatesToMoviesOnSuccess()
    {
        RespondJson(HttpMethod.Post, "/api/movies", new { id = 1, title = "New Movie" });
        var cut = Ctx.RenderComponent<Create>();

        // Fill in valid form data
        var titleInput = cut.Find("input#title");
        titleInput.Change("New Movie");

        var genreInput = cut.Find("input#genre");
        genreInput.Change("Sci-Fi");

        var priceInput = cut.Find("input#price");
        priceInput.Change("14.99");

        var ratingSelect = cut.Find("select#rating");
        ratingSelect.Change("PG-13");

        // Submit the form
        cut.Find("button[type='submit']").Click();

        // Should navigate back to /movies on success
        cut.WaitForAssertion(
            () => Assert.Contains("/movies", NavManager.Uri),
            timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_SubmitConflict_ShowsErrorMessage()
    {
        RespondJson(
            HttpMethod.Post, "/api/movies",
            new { message = "A movie with this title already exists." },
            HttpStatusCode.Conflict
        );

        var cut = Ctx.RenderComponent<Create>();

        var titleInput = cut.Find("input#title");
        titleInput.Change("Duplicate Movie");

        var genreInput = cut.Find("input#genre");
        genreInput.Change("Sci-Fi");

        var priceInput = cut.Find("input#price");
        priceInput.Change("14.99");

        var ratingSelect = cut.Find("select#rating");
        ratingSelect.Change("PG-13");

        cut.Find("button[type='submit']").Click();

        cut.WaitForState(() => cut.Markup.Contains("already exists"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("A movie with this title already exists.", cut);
    }

    [Fact]
    public void Create_SubmitServerError_ShowsErrorMessage()
    {
        RespondEmpty(HttpMethod.Post, "/api/movies", HttpStatusCode.InternalServerError);

        var cut = Ctx.RenderComponent<Create>();

        var titleInput = cut.Find("input#title");
        titleInput.Change("Error Movie");

        var genreInput = cut.Find("input#genre");
        genreInput.Change("Sci-Fi");

        var priceInput = cut.Find("input#price");
        priceInput.Change("14.99");

        var ratingSelect = cut.Find("select#rating");
        ratingSelect.Change("PG-13");

        cut.Find("button[type='submit']").Click();

        cut.WaitForState(() => cut.Markup.Contains("error occurred"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("An error occurred while creating the movie.", cut);
    }

    [Fact]
    public void Create_ShowsErrorDiv_WhenErrorMessageIsSet()
    {
        // The error div is conditionally rendered — hidden initially, shown on error
        RespondEmpty(HttpMethod.Post, "/api/movies", HttpStatusCode.InternalServerError);

        var cut = Ctx.RenderComponent<Create>();

        // Initially no error shown
        AssertElementNotExists("div.text-danger.mt-2", cut);

        // Fill and submit
        cut.Find("input#title").Change("Fail");
        cut.Find("input#genre").Change("Sci-Fi");
        cut.Find("input#price").Change("10");
        cut.Find("select#rating").Change("PG");
        cut.Find("button[type='submit']").Click();

        cut.WaitForState(() => cut.Markup.Contains("error occurred"), timeout: TimeSpan.FromSeconds(2));
        AssertElementExists("div.text-danger.mt-2", cut);
    }
}
