using System.Security.Claims;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Pages.Classic.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebAppMovies.Tests.ClassicUiPages;

public class ClassicUsersPageTests : IDisposable
{
    private readonly string _dbName;
    private readonly BlazorWebAppMoviesContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IndexModel _pageModel;
    private readonly IServiceScope _scope;
    private readonly ClaimsPrincipal _adminPrincipal;

    public ClassicUsersPageTests()
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

        _context = _scope.ServiceProvider.GetRequiredService<BlazorWebAppMoviesContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        _pageModel = new IndexModel(_userManager);

        // Seed roles and admin
        var admin = SeedAdminAsync().GetAwaiter().GetResult();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id),
            new(ClaimTypes.Name, admin.UserName!),
            new(ClaimTypes.Email, admin.Email!),
            new("role", "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, "role");
        _adminPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = _adminPrincipal };
        _pageModel.PageContext = new PageContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        _scope.Dispose();
        _context.Dispose();
    }

    private async Task<User> SeedAdminAsync()
    {
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));

        var admin = new User
        {
            UserName = "admin@page.com",
            Email = "admin@page.com",
            Name = "Admin Page"
        };
        await _userManager.CreateAsync(admin, "Admin123!");
        await _userManager.AddToRoleAsync(admin, "Admin");
        return admin;
    }

    private async Task<User> SeedRegularUser(string email = "user@page.com", string name = "Regular User")
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            Name = name
        };
        await _userManager.CreateAsync(user, "User1234!");
        await _userManager.AddToRoleAsync(user, "User");
        return user;
    }

    // ── OnGet ───────────────────────────────────────────────────

    [Fact]
    public async Task OnGet_SetsCurrentUserId()
    {
        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.CurrentUserId);
    }

    [Fact]
    public async Task OnGet_IncludesAllUsersInJson()
    {
        await SeedRegularUser("alice@test.com", "Alice");
        await SeedRegularUser("bob@test.com", "Bob");

        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(_pageModel.UsersJson);
        Assert.NotNull(users);
        // admin + alice + bob
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task OnGet_IncludesCorrectRolesInJson()
    {
        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(_pageModel.UsersJson);
        Assert.NotNull(users);

        var adminEntry = users.Single(u => u["email"].ToString() == "admin@page.com");
        var rolesElement = (System.Text.Json.JsonElement)adminEntry["roles"];
        var roles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rolesElement.GetRawText());
        Assert.NotNull(roles);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public async Task OnGet_UsersJsonIsValidJsonArray()
    {
        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        Assert.StartsWith("[", _pageModel.UsersJson.Trim());
        Assert.EndsWith("]", _pageModel.UsersJson.Trim());
    }

    [Fact]
    public async Task OnGet_UsersJsonContainsIdField()
    {
        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        Assert.Contains("\"id\"", _pageModel.UsersJson);
    }

    [Fact]
    public async Task OnGet_UsersJson_WhenNoUsers_ReturnsEmptyArray()
    {
        // Remove all users
        var allUsers = await _userManager.Users.ToListAsync();
        foreach (var u in allUsers)
            await _userManager.DeleteAsync(u);

        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        Assert.Equal("[]", _pageModel.UsersJson.Trim());
    }

    [Fact]
    public async Task OnGet_CurrentUserId_IsValidGuid()
    {
        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.CurrentUserId);
        Assert.True(Guid.TryParse(_pageModel.CurrentUserId, out _));
    }

    [Fact]
    public async Task OnGet_UsersOrderedByNameInJson()
    {
        await SeedRegularUser("z@test.com", "Zelda");
        await SeedRegularUser("a@test.com", "Aaron");

        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(_pageModel.UsersJson);
        Assert.NotNull(users);
        Assert.Equal("Aaron", users[0]["name"].ToString());
        Assert.Equal("Zelda", users[^1]["name"].ToString());
    }

    // ── Page authorization (the [Authorize(Roles = "Admin")] attribute) ──

    [Fact]
    public void PageModel_HasAuthorizeAttribute()
    {
        var attr = typeof(IndexModel).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(attr);
        var authAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)attr[0];
        Assert.Equal("Admin", authAttr.Roles);
    }

    // ── Self-delete guard ────────────────────────────────────────

    [Fact]
    public async Task OnGet_CanDeleteUsers_IsTrue_WhenUserFound()
    {
        await _pageModel.OnGet();

        Assert.True(_pageModel.CanDeleteUsers);
    }

    [Fact]
    public async Task OnGet_CanDeleteUsers_IsFalse_WhenUserNotFound()
    {
        // Simulate UserManager.GetUserAsync returning null by
        // setting the user to a non-existent principal
        var emptyPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _pageModel.PageContext.HttpContext!.User = emptyPrincipal;

        await _pageModel.OnGet();

        Assert.False(_pageModel.CanDeleteUsers);
        Assert.Null(_pageModel.CurrentUserId);
    }

    // ── UserDto structure ───────────────────────────────────────

    [Fact]
    public async Task OnGet_UserDto_HasIsAdminProperty()
    {
        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(_pageModel.UsersJson);
        Assert.NotNull(users);
        var adminEntry = users.Single(u => u["email"].ToString() == "admin@page.com");
        var isAdmin = ((System.Text.Json.JsonElement)adminEntry["isAdmin"]).GetBoolean();
        Assert.True(isAdmin);
    }

    [Fact]
    public async Task OnGet_UserDto_RegularUserIsNotAdmin()
    {
        var user = await SeedRegularUser();

        await _pageModel.OnGet();

        Assert.NotNull(_pageModel.UsersJson);
        var users = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(_pageModel.UsersJson);
        Assert.NotNull(users);
        var userEntry = users.Single(u => u["email"].ToString() == user.Email);
        var isAdmin = ((System.Text.Json.JsonElement)userEntry["isAdmin"]).GetBoolean();
        Assert.False(isAdmin);
    }
}
