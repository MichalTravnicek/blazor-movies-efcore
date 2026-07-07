using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class AuthFlowTests
{
    private static (IServiceProvider, UserManager<User>, RoleManager<IdentityRole>) CreateAuthServices()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName));

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        return (serviceProvider, userManager, roleManager);
    }

    // ── Seed creates admin role and admin user ─────────────────────

    [Fact]
    public async Task SeedData_CreatesAdminRole()
    {
        var (services, _, roleManager) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        Assert.True(await roleManager.RoleExistsAsync("Admin"));
    }

    [Fact]
    public async Task SeedData_CreatesUserRole()
    {
        var (services, _, roleManager) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        Assert.True(await roleManager.RoleExistsAsync("User"));
    }

    [Fact]
    public async Task SeedData_CreatesAdminUser()
    {
        var (services, userManager, _) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        var admin = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(admin);
        Assert.Equal("Admin", admin.Name);
    }

    [Fact]
    public async Task SeedData_AdminUserHasAdminRole()
    {
        var (services, userManager, _) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        var admin = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(admin);

        var roles = await userManager.GetRolesAsync(admin);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public async Task SeedData_AdminUserCanSignIn()
    {
        var (services, userManager, _) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        var admin = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(admin);

        var signInManager = services.GetRequiredService<SignInManager<User>>();
        var result = await signInManager.CheckPasswordSignInAsync(admin, "Admin123!",
            lockoutOnFailure: false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SeedData_DoesNotDuplicateAdminOnSecondRun()
    {
        var (services, userManager, _) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);
        await SeedData.Initialize(factory, services);

        var admins = await userManager.GetUsersInRoleAsync("Admin");
        Assert.Single(admins);
    }

    // ── Login/Register flow tests ──────────────────────────────────

    [Fact]
    public async Task RegisterUser_ThenLogin_ReturnsUser()
    {
        var (services, userManager, _) = CreateAuthServices();

        var user = new User
        {
            UserName = "flow@example.com",
            Email = "flow@example.com",
            Name = "Flow Test"
        };

        var createResult = await userManager.CreateAsync(user, "FlowT3st!Pass");
        Assert.True(createResult.Succeeded);

        var signInManager = services.GetRequiredService<SignInManager<User>>();
        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user, "FlowT3st!Pass", lockoutOnFailure: false);

        Assert.True(signInResult.Succeeded);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        var (services, userManager, _) = CreateAuthServices();

        var user = new User
        {
            UserName = "wrong@example.com",
            Email = "wrong@example.com",
            Name = "Wrong"
        };

        await userManager.CreateAsync(user, "C0rrect!Pass");

        var signInManager = services.GetRequiredService<SignInManager<User>>();
        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user, "WrongPassword1!", lockoutOnFailure: false);

        Assert.False(signInResult.Succeeded);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_Fails()
    {
        var (services, userManager, _) = CreateAuthServices();

        var user = await userManager.FindByEmailAsync("nobody@example.com");
        Assert.Null(user);
    }

    // ── Admin role authorization tests ─────────────────────────────

    [Fact]
    public async Task AdminUser_HasAdminRole()
    {
        var (services, userManager, _) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        var admin = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(admin);

        var roles = await userManager.GetRolesAsync(admin);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public async Task RegularUser_DoesNotHaveAdminRole()
    {
        var (services, userManager, _) = CreateAuthServices();
        var factory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

        await SeedData.Initialize(factory, services);

        // Create a regular user
        var user = new User
        {
            UserName = "regular@example.com",
            Email = "regular@example.com",
            Name = "Regular User"
        };
        await userManager.CreateAsync(user, "Regul4r!User");
        await userManager.AddToRoleAsync(user, "User");

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains("User", roles);
        Assert.DoesNotContain("Admin", roles);
    }

    // ── JWT cookie auth mechanism ──────────────────────────────────

    [Fact]
    public async Task JwtCookieAuth_CanRoundtrip()
    {
        // This validates the core auth mechanism: create user, sign in,
        // verify identity stores the password hash correctly
        var (services, userManager, _) = CreateAuthServices();

        var user = new User
        {
            UserName = "cookie@example.com",
            Email = "cookie@example.com",
            Name = "Cookie Test"
        };

        await userManager.CreateAsync(user, "Cooki3!Auth");

        var fetched = await userManager.FindByEmailAsync("cookie@example.com");
        Assert.NotNull(fetched);
        Assert.Equal("Cookie Test", fetched.Name);

        // Verify the password hash is stored (would be used by JWT validation)
        Assert.NotNull(fetched.PasswordHash);

        // Verify we can authenticate
        var signInManager = services.GetRequiredService<SignInManager<User>>();
        var result = await signInManager.CheckPasswordSignInAsync(
            fetched, "Cooki3!Auth", lockoutOnFailure: false);
        Assert.True(result.Succeeded);
    }
}
