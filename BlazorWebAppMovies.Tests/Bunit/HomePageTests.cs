using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using BlazorWebAppMovies.Components.Pages;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Tests.Bunit;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// bUnit tests for Home.razor — auth state rendering, login form, role-based messages.
/// </summary>
public class HomePageTests : BunitTestBase
{
    /// <summary>
    /// Creates a Mock UserManager that returns the given display name from GetUserAsync.
    /// </summary>
    private static Mock<UserManager<User>> CreateUserManagerMock(string displayName)
    {
        var store = Mock.Of<IUserStore<User>>();
        var mgr = new Mock<UserManager<User>>(
            store,
            Mock.Of<Microsoft.Extensions.Options.IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<User>>(),
            new List<IUserValidator<User>>(),
            new List<IPasswordValidator<User>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<User>>>()
        );

        mgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new User { Name = displayName, UserName = "test@user.com" });

        return mgr;
    }

    [Fact]
    public void HomePage_Unauthenticated_ShowsSignInForm()
    {
        SetAuthState(false);

        var cut = Ctx.RenderComponent<Home>();

        AssertContains("Sign In", cut);
        AssertContains("Email", cut);
        AssertContains("Password", cut);
        AssertElementExists("input#email", cut);
        AssertElementExists("input#password", cut);
    }

    [Fact]
    public void HomePage_Unauthenticated_HidesWelcomeMessage()
    {
        SetAuthState(false);

        var cut = Ctx.RenderComponent<Home>();

        AssertNotContains("Welcome", cut);
        AssertNotContains("You are logged in", cut);
    }

    [Fact]
    public void HomePage_AuthenticatedUser_ShowsWelcomeMessage()
    {
        var userManagerMock = CreateUserManagerMock("Test User");
        Ctx.Services.AddSingleton(userManagerMock.Object);
        SetAuthState(isAuthenticated: true, role: "User", userName: "test@user.com");

        var cut = Ctx.RenderComponent<Home>();

        cut.WaitForState(() => cut.Markup.Contains("Welcome"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("You are logged in as test@user.com", cut);
    }

    [Fact]
    public void HomePage_AuthenticatedUser_HidesSignInForm()
    {
        var userManagerMock = CreateUserManagerMock("Test User");
        Ctx.Services.AddSingleton(userManagerMock.Object);
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = Ctx.RenderComponent<Home>();

        cut.WaitForState(() => cut.Markup.Contains("Welcome"), timeout: TimeSpan.FromSeconds(2));
        AssertNotContains("Sign In", cut);
    }

    [Fact]
    public void HomePage_AdminUser_SeesAdminPrivilegesMessage()
    {
        var userManagerMock = CreateUserManagerMock("Admin User");
        Ctx.Services.AddSingleton(userManagerMock.Object);
        SetAuthState(isAuthenticated: true, role: "Admin");

        var cut = Ctx.RenderComponent<Home>();

        cut.WaitForState(() => cut.Markup.Contains("Welcome"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("You have administrator privileges", cut);
    }

    [Fact]
    public void HomePage_RegularUser_DoesNotSeeAdminPrivilegesMessage()
    {
        var userManagerMock = CreateUserManagerMock("Regular User");
        Ctx.Services.AddSingleton(userManagerMock.Object);
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = Ctx.RenderComponent<Home>();

        cut.WaitForState(() => cut.Markup.Contains("Welcome"), timeout: TimeSpan.FromSeconds(2));
        AssertNotContains("administrator privileges", cut);
    }

    [Fact]
    public void HomePage_RegularUser_SeesUserPrivilegesMessage()
    {
        var userManagerMock = CreateUserManagerMock("Regular User");
        Ctx.Services.AddSingleton(userManagerMock.Object);
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = Ctx.RenderComponent<Home>();

        cut.WaitForState(() => cut.Markup.Contains("Welcome"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("You have user privileges", cut);
        AssertContains("Go edit movies!", cut);
    }

    [Fact]
    public void HomePage_ShowsUiSwitcher_WhenAuthenticated()
    {
        var userManagerMock = CreateUserManagerMock("Test User");
        Ctx.Services.AddSingleton(userManagerMock.Object);
        SetAuthState(isAuthenticated: true, role: "User");

        var cut = Ctx.RenderComponent<Home>();

        cut.WaitForState(() => cut.Markup.Contains("Welcome"), timeout: TimeSpan.FromSeconds(2));
        AssertContains("Choose Your UI", cut);
        AssertContains("Blazor UI", cut);
        AssertContains("Classic UI (jQuery)", cut);
    }
}
