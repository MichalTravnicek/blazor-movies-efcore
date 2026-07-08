using BlazorWebAppMovies.Pages.Classic.Movies;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlazorWebAppMovies.Tests.ClassicUiPages;

public class ClassicMoviesPageTests
{
    [Fact]
    public void OnGet_DoesNotThrow()
    {
        var pageModel = new IndexModel();

        // Should not throw — the page model is intentionally simple
        var exception = Record.Exception(() => pageModel.OnGet());

        Assert.Null(exception);
    }

    [Fact]
    public void PageModel_HasAllowAnonymousAttribute()
    {
        var attr = typeof(IndexModel).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        Assert.NotEmpty(attr);
    }
}
