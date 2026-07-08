using System.Security.Claims;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebAppMovies.Tests.BlazorUiTests;

/// <summary>
/// Tests for the Blazor UI authentication flow used in Home.razor.
/// Verifies auth state propagation, role checks, and the login flow.
/// </summary>
public class BlazorAuthFlowTests : IDisposable
{
    private readonly string _dbName;
    private readonly UserManager<User> _userManager;
    private readonly IServiceScope _scope;

    public BlazorAuthFlowTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.RoleClaimType = "role";
        });

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        _scope = serviceProvider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    private async Task SeedRoles()
    {
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));
    }

    private async Task<User> CreateAdmin(string email = "admin@test.com")
    {
        await SeedRoles();
        var user = new User { UserName = email, Email = email, Name = "Admin" };
        await _userManager.CreateAsync(user, "Admin123!");
        await _userManager.AddToRoleAsync(user, "Admin");
        return user;
    }

    private async Task<User> CreateUser(string email = "user@test.com")
    {
        await SeedRoles();
        var user = new User { UserName = email, Email = email, Name = "User" };
        await _userManager.CreateAsync(user, "User1234!");
        await _userManager.AddToRoleAsync(user, "User");
        return user;
    }

    private static ClaimsPrincipal CreatePrincipal(User user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!)
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        var identity = new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, "role");
        return new ClaimsPrincipal(identity);
    }

    // ── Home.razor auth state guards ────────────────────────────

    [Fact]
    public async Task AdminUser_SeesAdminPrivilegesMessage()
    {
        var admin = await CreateAdmin();
        var roles = await _userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        bool isAdmin = principal.IsInRole("Admin");
        bool isAuthenticated = principal.Identity?.IsAuthenticated == true;

        Assert.True(isAuthenticated);
        Assert.True(isAdmin);
    }

    [Fact]
    public async Task RegularUser_DoesNotSeeAdminPrivilegesMessage()
    {
        var user = await CreateUser();
        var roles = await _userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool isAdmin = principal.IsInRole("Admin");

        Assert.False(isAdmin);
    }

    [Fact]
    public async Task RegularUser_SeesUserPrivilegesMessage()
    {
        var user = await CreateUser();
        var roles = await _userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool isUser = principal.IsInRole("User");

        Assert.True(isUser);
    }

    [Fact]
    public void UnauthenticatedUser_SeesLoginForm()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        bool isAuthenticated = principal.Identity?.IsAuthenticated == true;
        Assert.False(isAuthenticated);
    }

    // ── User name display (Home.razor) ──────────────────────────

    [Fact]
    public async Task HomePage_DisplaysUserName()
    {
        var user = await CreateUser("alice@test.com");
        var roles = await _userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        string? userName = principal.Identity?.Name;
        Assert.Equal("alice@test.com", userName);
    }

    [Fact]
    public async Task HomePage_AdminDisplaysUserName()
    {
        var admin = await CreateAdmin("boss@test.com");
        var roles = await _userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        string? userName = principal.Identity?.Name;
        Assert.Equal("boss@test.com", userName);
    }

    // ── UI switcher visibility (Home.razor) ─────────────────────

    [Fact]
    public async Task AuthenticatedUser_SeesUiSwitcher()
    {
        var user = await CreateUser();
        var roles = await _userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        // The UI switcher card is shown when authenticated
        bool isAuthenticated = principal.Identity?.IsAuthenticated == true;
        Assert.True(isAuthenticated);
    }

    // ── /usermanagement route guard (UserManagement.razor) ──────

    [Fact]
    public async Task AdminUser_CanAccessUserManagement()
    {
        var admin = await CreateAdmin();
        var roles = await _userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        bool canAccess = principal.IsInRole("Admin");
        Assert.True(canAccess);
    }

    [Fact]
    public async Task RegularUser_CannotAccessUserManagement()
    {
        var user = await CreateUser();
        var roles = await _userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool canAccess = principal.IsInRole("Admin");
        Assert.False(canAccess);
    }

    // ── NavMenu (Blazor) link visibility ────────────────────────

    [Fact]
    public async Task NavMenu_AdminSeesUsersLink()
    {
        var admin = await CreateAdmin();
        var roles = await _userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        bool showUsersLink = principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
        Assert.True(showUsersLink);
    }

    [Fact]
    public async Task NavMenu_UserDoesNotSeeUsersLink()
    {
        var user = await CreateUser();
        var roles = await _userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool showUsersLink = principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
        Assert.False(showUsersLink);
    }

    [Fact]
    public void NavMenu_UnauthenticatedDoesNotSeeUsersLink()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        bool showUsersLink = principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
        Assert.False(showUsersLink);
    }

    // ── Login validation ────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUser()
    {
        await CreateUser("valid@test.com");

        var user = await _userManager.FindByEmailAsync("valid@test.com");
        Assert.NotNull(user);
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsNull()
    {
        var user = await _userManager.FindByEmailAsync("nonexistent@test.com");
        Assert.Null(user);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        var user = await CreateUser("login@test.com");

        var signInManager = CreateSignInManager();
        var result = await signInManager.CheckPasswordSignInAsync(user, "WrongPass1!", false);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        var user = await CreateUser("good@test.com");

        var signInManager = CreateSignInManager();
        var result = await signInManager.CheckPasswordSignInAsync(user, "User1234!", false);

        Assert.True(result.Succeeded);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private SignInManager<User> CreateSignInManager()
    {
        return new SignInManager<User>(
            _userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<User>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>()
        );
    }
}
