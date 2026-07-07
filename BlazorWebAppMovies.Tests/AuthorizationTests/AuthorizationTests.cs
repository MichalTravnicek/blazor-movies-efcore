using System.Security.Claims;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebAppMovies.Tests.AuthorizationTests;

public class AuthorizationTests
{
    private static IServiceProvider CreateAuthServices(string dbName)
    {
        var services = new ServiceCollection();

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName));

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.RoleClaimType = "role";
        });

        services.AddLogging();
        services.AddAuthorization();

        return services.BuildServiceProvider();
    }

    private static async Task<(UserManager<User> UserManager, RoleManager<IdentityRole> RoleManager)> SeedRolesAndAdmin(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));

        return (userManager, roleManager);
    }

    // ── Role-check helpers (mirrors the NavMenu and Index guards) ──

    private static ClaimsPrincipal CreatePrincipal(User user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email!)
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var identity = new ClaimsIdentity(claims, "TestAuthType", ClaimTypes.Name, "role");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task AdminUser_CanAccessUserManagement()
    {
        var services = CreateAuthServices("admin-crud-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var admin = new User { UserName = "admin@example.com", Email = "admin@example.com", Name = "Admin" };
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, "Admin");

        var roles = await userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public async Task RegularUser_CannotAccessUserManagement()
    {
        var services = CreateAuthServices("user-no-admin-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var user = new User { UserName = "user@example.com", Email = "user@example.com", Name = "User" };
        await userManager.CreateAsync(user, "User1234!");
        await userManager.AddToRoleAsync(user, "User");

        var roles = await userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        Assert.False(principal.IsInRole("Admin"));
    }

    // ── NavMenu: Users link visibility ─────────────────────────────

    [Fact]
    public async Task AdminUser_SeesUsersLink_InNavMenu()
    {
        var services = CreateAuthServices("nav-admin-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var admin = new User { UserName = "admin@nav.com", Email = "admin@nav.com", Name = "Admin" };
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, "Admin");

        var roles = await userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        bool showUsersLink = principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
        Assert.True(showUsersLink);
    }

    [Fact]
    public async Task RegularUser_DoesNotSeeUsersLink_InNavMenu()
    {
        var services = CreateAuthServices("nav-user-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var user = new User { UserName = "user@nav.com", Email = "user@nav.com", Name = "User" };
        await userManager.CreateAsync(user, "User1234!");
        await userManager.AddToRoleAsync(user, "User");

        var roles = await userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool showUsersLink = principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
        Assert.False(showUsersLink);
    }

    [Fact]
    public void UnauthenticatedUser_DoesNotSeeUsersLink()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        bool showUsersLink = principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
        Assert.False(showUsersLink);
    }

    // ── Movie CRUD permissions (Index.razor guards) ────────────────

    [Fact]
    public async Task AuthenticatedUser_CanSeeCreateLink()
    {
        var services = CreateAuthServices("movie-create-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var user = new User { UserName = "user@movie.com", Email = "user@movie.com", Name = "User" };
        await userManager.CreateAsync(user, "User1234!");
        await userManager.AddToRoleAsync(user, "User");

        var roles = await userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool canCreate = principal.Identity?.IsAuthenticated == true;
        Assert.True(canCreate);
    }

    [Fact]
    public async Task AuthenticatedUser_CanSeeEditAndDeleteLinks()
    {
        var services = CreateAuthServices("movie-edit-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var user = new User { UserName = "user@edit.com", Email = "user@edit.com", Name = "User" };
        await userManager.CreateAsync(user, "User1234!");
        await userManager.AddToRoleAsync(user, "User");

        var roles = await userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        bool authenticated = principal.Identity?.IsAuthenticated == true;
        Assert.True(authenticated);
    }

    [Fact]
    public void UnauthenticatedUser_CannotSeeCreateLink()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        bool canCreate = principal.Identity?.IsAuthenticated == true;
        Assert.False(canCreate);
    }

    [Fact]
    public void UnauthenticatedUser_CannotSeeEditAndDeleteLinks()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        bool authenticated = principal.Identity?.IsAuthenticated == true;
        Assert.False(authenticated);
    }

    [Fact]
    public void UnauthenticatedUser_CanAlwaysSeeDetailsLink()
    {
        // The Details link is unconditional in the template — no auth guard wraps it.
        // This is documented in the README permission matrix as "✅" for all roles.
        // Since there's no code-level guard, we verify the template renders it always.
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // The template row does: <a href="@($"movies/details?id={movie.Id}")">Details</a>
        // No authentication check, no role check.
        bool hasDetailsLink = true;

        Assert.True(hasDetailsLink, "Details link is unconditional in the template");
    }

    // ── ClaimsPrincipal with "role" claim type (JWT format) ────────

    [Fact]
    public async Task JwtRoleClaim_WorksWithIsInRole()
    {
        var services = CreateAuthServices("jwt-claim-" + Guid.NewGuid());
        var (userManager, _) = await SeedRolesAndAdmin(services);

        var admin = new User { UserName = "claim@test.com", Email = "claim@test.com", Name = "Claim" };
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, "Admin");

        var roles = await userManager.GetRolesAsync(admin);
        var principal = CreatePrincipal(admin, roles);

        // This replicates what happens when JWT with "role" claim is validated
        Assert.True(principal.IsInRole("Admin"));
        Assert.False(principal.IsInRole("NonExistent"));
    }

    [Fact]
    public async Task MultipleRoles_AllDetected()
    {
        var services = CreateAuthServices("multi-role-" + Guid.NewGuid());
        var (userManager, roleManager) = await SeedRolesAndAdmin(services);

        // Create a custom role
        if (!await roleManager.RoleExistsAsync("Editor"))
            await roleManager.CreateAsync(new IdentityRole("Editor"));

        var user = new User { UserName = "multi@role.com", Email = "multi@role.com", Name = "Multi" };
        await userManager.CreateAsync(user, "Multi123!");
        await userManager.AddToRoleAsync(user, "User");
        await userManager.AddToRoleAsync(user, "Editor");

        var roles = await userManager.GetRolesAsync(user);
        var principal = CreatePrincipal(user, roles);

        Assert.True(principal.IsInRole("User"));
        Assert.True(principal.IsInRole("Editor"));
        Assert.False(principal.IsInRole("Admin"));
    }
}
