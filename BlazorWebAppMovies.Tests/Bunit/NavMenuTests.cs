using Bunit;
using Microsoft.AspNetCore.Components;
using BlazorWebAppMovies.Components.Layout;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for NavMenu.razor — role-based link visibility.
/// </summary>
public class NavMenuTests : BunitTestBase
{
    [Fact]
    public void NavMenu_ShowsHomeWeatherMoviesAndClassicUiLinks_Always()
    {
        SetAuthState(false);

        var cut = Ctx.RenderComponent<NavMenu>();

        // All users see these links regardless of auth
        AssertContains("Home", cut);
        AssertContains("Weather", cut);
        AssertContains("Movies", cut);
        AssertContains("Classic UI", cut);
    }

    [Fact]
    public void NavMenu_AdminSeesUsersLink()
    {
        SetAuthState(isAuthenticated: true, role: "Admin");

        var cut = Ctx.RenderComponent<NavMenu>();

        AssertContains("Users", cut);
    }

    [Fact]
    public void NavMenu_RegularUserDoesNotSeeUsersLink()
    {
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = Ctx.RenderComponent<NavMenu>();

        AssertNotContains("Users", cut);
    }

    [Fact]
    public void NavMenu_UnauthenticatedDoesNotSeeUsersLink()
    {
        SetAuthState(false);

        var cut = Ctx.RenderComponent<NavMenu>();

        AssertNotContains("Users", cut);
    }

    [Fact]
    public void NavMenu_UsersLinkNavigatesToUserManagement()
    {
        SetAuthState(isAuthenticated: true, role: "Admin");

        var cut = Ctx.RenderComponent<NavMenu>();

        var usersLink = cut.Find("a.nav-link[href='usermanagement']");
        Assert.NotNull(usersLink);
        Assert.Equal("usermanagement", usersLink.GetAttribute("href"));
    }
}
